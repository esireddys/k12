using K12.Application.Abstractions;
using K12.Domain.Content;
using K12.Domain.Content.Answers;

namespace K12.Application.Practice.Sets;

public sealed record ResponseItem(Guid QuestionId, string? Response);

/// <summary>Scores responses and reveals the key. Practice sends one response at a time; an exam sends all of them at Finish.</summary>
public sealed record ScoreResponsesCommand(IReadOnlyList<ResponseItem> Responses);

/// <param name="Answered">False when the response was blank: it counts as wrong.</param>
/// <param name="CorrectAnswer">The key, for example "C" or "144".</param>
/// <param name="CorrectChoiceText">For multiple choice, the text of the correct option when there is one.</param>
/// <param name="Explanation">Why, when the question has one.</param>
public sealed record ScoredResponse(
    Guid QuestionId, bool Answered, bool IsCorrect, bool Understood,
    string CorrectAnswer, string? CorrectChoiceText, string? Explanation);

public sealed record ScoreResult(int Correct, int Total, IReadOnlyList<ScoredResponse> Items);

/// <summary>
/// Checks each response against its question's answer spec and returns the key and explanation with it.
/// The key is only ever sent back for a question the student has just answered or finished.
/// Guests use this and nothing is saved; a kid's answers go through <c>RecordAnswersHandler</c>, which saves them.
/// </summary>
public sealed class ScoreResponsesHandler(IQuestionRepository questions) : ICommandHandler<ScoreResponsesCommand, ScoreResult>
{
    public const int MaxResponses = Scoring.MaxResponses;

    public async Task<Result<ScoreResult>> HandleAsync(ScoreResponsesCommand command, CancellationToken ct = default)
    {
        var scored = await Scoring.ScoreAsync(questions, command.Responses, ct);
        return scored.IsSuccess ? scored.Value!.Result : scored.Error!;
    }
}

/// <summary>A scored batch and the questions it was scored against.</summary>
internal sealed record ScoredBatch(ScoreResult Result, IReadOnlyDictionary<Guid, Question> Questions);

/// <summary>Scoring shared by guests and kids. Only Live questions can be scored.</summary>
internal static class Scoring
{
    public const int MaxResponses = 100;

    public static async Task<Result<ScoredBatch>> ScoreAsync(
        IQuestionRepository questions, IReadOnlyList<ResponseItem> responses, CancellationToken ct)
    {
        if (responses.Count == 0) return Error.Validation("score.empty", "Nothing to score.");
        if (responses.Count > MaxResponses) return Error.Validation("score.too_many", $"Send at most {MaxResponses} answers.");

        var ids = responses.Select(r => new QuestionId(r.QuestionId)).Distinct().ToList();
        var byId = (await questions.ListByIdsAsync(ids, ct)).Where(q => q.IsVisibleToStudents).ToDictionary(q => q.Id.Value);
        var missing = responses.FirstOrDefault(r => !byId.ContainsKey(r.QuestionId));
        if (missing is not null) return Error.NotFound("question.not_found", "No such question.");

        var items = responses.Select(r =>
        {
            var q = byId[r.QuestionId];
            var content = QuestionContent.Read(q.ContentJson);
            var answered = !string.IsNullOrWhiteSpace(r.Response);
            var check = answered ? q.Check(r.Response) : new CheckResult(false, true, "");
            var choiceText = q.Answer.Kind == AnswerKind.Choice
                ? content.Choices.FirstOrDefault(c => string.Equals(c.Label, q.Answer.Value, StringComparison.OrdinalIgnoreCase))?.Text
                : null;
            return new ScoredResponse(
                q.Id.Value, answered, check.IsCorrect, check.Understood, q.Answer.Value,
                string.IsNullOrWhiteSpace(choiceText) || content.TextNeedsReview ? null : choiceText,
                content.Explanation);
        }).ToList();

        return new ScoredBatch(new ScoreResult(items.Count(i => i.IsCorrect), items.Count, items), byId);
    }
}
