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
/// Recording the result for a kid's progress comes with the practice history.
/// </summary>
public sealed class ScoreResponsesHandler(IQuestionRepository questions) : ICommandHandler<ScoreResponsesCommand, ScoreResult>
{
    public const int MaxResponses = 100;

    public async Task<Result<ScoreResult>> HandleAsync(ScoreResponsesCommand command, CancellationToken ct = default)
    {
        if (command.Responses.Count == 0) return Error.Validation("score.empty", "Nothing to score.");
        if (command.Responses.Count > MaxResponses) return Error.Validation("score.too_many", $"Send at most {MaxResponses} answers.");

        var ids = command.Responses.Select(r => new QuestionId(r.QuestionId)).Distinct().ToList();
        var byId = (await questions.ListByIdsAsync(ids, ct)).Where(q => q.IsVisibleToStudents).ToDictionary(q => q.Id.Value);
        var missing = command.Responses.FirstOrDefault(r => !byId.ContainsKey(r.QuestionId));
        if (missing is not null) return Error.NotFound("question.not_found", "No such question.");

        var items = command.Responses.Select(r =>
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

        return new ScoreResult(items.Count(i => i.IsCorrect), items.Count, items);
    }
}
