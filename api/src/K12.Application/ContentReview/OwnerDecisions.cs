using System.Text.Json;
using System.Text.Json.Nodes;
using K12.Application.Abstractions;
using K12.Domain.Common;
using K12.Domain.Content;
using K12.Domain.Content.Answers;
using K12.Domain.Content.Review;

namespace K12.Application.ContentReview;

/// <summary>
/// An owner approves a question. With <paramref name="Content"/>, <paramref name="Answer"/> and <paramref name="Difficulty"/>
/// the owner's edits go Live; without them the question goes Live as it stands.
/// </summary>
/// <param name="Version">The version the owner looked at, so two owners cannot overwrite each other.</param>
public sealed record ApproveQuestionCommand(
    Guid QuestionId, int Version, string ApprovedBy,
    JsonElement? Content = null, AnswerSpec? Answer = null, Difficulty? Difficulty = null, string? Note = null);

public sealed record RejectQuestionCommand(Guid QuestionId, int Version, string RejectedBy, string Reason);

/// <summary>Makes a question Live. Only an owner reaches this use case; the API enforces the role.</summary>
public sealed class ApproveQuestionHandler(IQuestionRepository questions, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<ApproveQuestionCommand, ReviewStepResponse>
{
    public async Task<Result<ReviewStepResponse>> HandleAsync(ApproveQuestionCommand command, CancellationToken ct = default)
    {
        var (question, error) = await OwnerDecisionGuard.LoadAsync(questions, command.QuestionId, command.Version, ct);
        if (error is not null) return error;

        var edits = command.Content is null && command.Answer is null && command.Difficulty is null
            ? null
            : new OwnerEdits(
                command.Content is { } c ? OwnerDecisionGuard.Reviewed(c) : question!.ContentJson,
                command.Answer ?? question!.Answer,
                command.Difficulty ?? question!.Difficulty);

        return await OwnerDecisionGuard.ApplyAsync(question!, unitOfWork, q =>
        {
            if (edits is null) q.Approve(command.ApprovedBy, clock.UtcNow, command.Note);
            else q.Approve(edits, command.ApprovedBy, clock.UtcNow, command.Note);
        }, ct);
    }
}

/// <summary>Keeps a question away from students, with the owner's reason.</summary>
public sealed class RejectQuestionHandler(IQuestionRepository questions, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<RejectQuestionCommand, ReviewStepResponse>
{
    public async Task<Result<ReviewStepResponse>> HandleAsync(RejectQuestionCommand command, CancellationToken ct = default)
    {
        var (question, error) = await OwnerDecisionGuard.LoadAsync(questions, command.QuestionId, command.Version, ct);
        if (error is not null) return error;
        return await OwnerDecisionGuard.ApplyAsync(question!, unitOfWork,
            q => q.Reject(command.RejectedBy, clock.UtcNow, command.Reason), ct);
    }
}

internal static class OwnerDecisionGuard
{
    public static async Task<(Question? Question, Error? Error)> LoadAsync(
        IQuestionRepository questions, Guid id, int version, CancellationToken ct)
    {
        var question = await questions.GetAsync(new QuestionId(id), ct);
        if (question is null) return (null, Error.NotFound("question.not_found", "No such question."));
        if (question.Version != version)
            return (null, Error.Conflict("review.stale", $"Someone changed this question (now version {question.Version}). Reload it."));
        if (question.Status != QuestionStatus.AwaitingOwner)
            return (null, Error.Conflict("review.wrong_state", $"The question is {question.Status}, not waiting for an owner."));
        return (question, null);
    }

    public static async Task<Result<ReviewStepResponse>> ApplyAsync(
        Question question, IUnitOfWork unitOfWork, Action<Question> decide, CancellationToken ct)
    {
        try
        {
            decide(question);
        }
        catch (DomainException e)
        {
            return Error.Validation("review.invalid", e.Message);
        }

        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            return Error.Conflict("review.concurrent", "Someone else decided on this question at the same time. Reload it.");
        }

        return new ReviewStepResponse(question.Id.Value, question.Version, question.Status);
    }

    /// <summary>An owner has now checked the text against the picture, so it no longer carries the import's review flag.</summary>
    public static string Reviewed(JsonElement content)
    {
        if (content.ValueKind != JsonValueKind.Object) return content.GetRawText();
        var node = JsonNode.Parse(content.GetRawText())!.AsObject();
        node.Remove("textNeedsReview");
        return node.ToJsonString();
    }
}
