using System.Text.Json;
using K12.Application.Abstractions;
using K12.Domain.Common;
using K12.Domain.Content;
using K12.Domain.Content.Review;

namespace K12.Application.ContentReview;

/// <param name="Version">The question version the reviewer looked at. A newer version means its findings are stale.</param>
public sealed record RecordAiReviewCommand(
    Guid QuestionId, int Version, string Reviewer, AiVerdict Verdict, bool AnswerKeyAgrees, string SolvedAnswer,
    Difficulty SuggestedDifficulty, JsonElement ProposedContent, string Summary, IReadOnlyList<AiFinding> Findings);

public sealed record ReviewStepResponse(Guid QuestionId, int Version, QuestionStatus Status);

/// <summary>Stores the AI reviewer's findings and moves the question to the owners' queue.</summary>
public sealed class RecordAiReviewHandler(IQuestionRepository questions, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<RecordAiReviewCommand, ReviewStepResponse>
{
    public async Task<Result<ReviewStepResponse>> HandleAsync(RecordAiReviewCommand command, CancellationToken ct = default)
    {
        var question = await questions.GetAsync(new QuestionId(command.QuestionId), ct);
        if (question is null) return Error.NotFound("question.not_found", "No such question.");
        if (question.Version != command.Version)
            return Error.Conflict("review.stale", $"The question is now version {question.Version}; review it again.");
        if (command.ProposedContent.ValueKind != JsonValueKind.Object)
            return Error.Validation("review.proposed_content", "The proposed content must be a JSON object.");

        try
        {
            question.RecordAiReview(new AiReview
            {
                Reviewer = command.Reviewer?.Trim() ?? "",
                ReviewedAt = clock.UtcNow,
                Verdict = command.Verdict,
                AnswerKeyAgrees = command.AnswerKeyAgrees,
                SolvedAnswer = command.SolvedAnswer?.Trim() ?? "",
                SuggestedDifficulty = command.SuggestedDifficulty,
                ProposedContentJson = command.ProposedContent.GetRawText(),
                Summary = command.Summary?.Trim() ?? "",
                Findings = command.Findings ?? [],
            });
        }
        catch (DomainException e) when (question.Status != QuestionStatus.InAiReview)
        {
            return Error.Conflict("review.wrong_state", e.Message);
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
            return Error.Conflict("review.concurrent", "The question changed while it was being reviewed.");
        }

        return new ReviewStepResponse(question.Id.Value, question.Version, question.Status);
    }
}
