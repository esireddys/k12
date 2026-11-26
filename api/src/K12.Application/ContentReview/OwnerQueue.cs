using K12.Application.Abstractions;
using K12.Domain.Content;
using K12.Domain.Content.Review;
using K12.Domain.Curriculum;

namespace K12.Application.ContentReview;

public sealed record GetOwnerQueueQuery(int Take = 500);

/// <summary>One line in the owners' queue.</summary>
public sealed record OwnerQueueItem(
    Guid Id, int Version, string ItemType, IReadOnlyList<string> SkillCodes, ReviewSource? Source,
    AiVerdict? Verdict, bool? AnswerKeyAgrees, int ProblemCount, int WarningCount, string? Summary,
    Difficulty Difficulty, Difficulty? SuggestedDifficulty);

public sealed record OwnerQueueResponse(IReadOnlyDictionary<QuestionStatus, int> Counts, IReadOnlyList<OwnerQueueItem> Items);

/// <summary>
/// Questions waiting for an owner, the ones the AI reviewer flagged first, then in test order.
/// The counts show how far the whole bank is through review.
/// </summary>
public sealed class GetOwnerQueueHandler(
    IQuestionRepository questions, ISkillRepository skills, IExamFormRepository forms, ICourseRepository courses)
    : IQueryHandler<GetOwnerQueueQuery, OwnerQueueResponse>
{
    public async Task<Result<OwnerQueueResponse>> HandleAsync(GetOwnerQueueQuery query, CancellationToken ct = default)
    {
        if (query.Take is < 1 or > 1000) return Error.Validation("review.take", "Take between 1 and 1000 questions.");
        var counts = await questions.CountByStatusAsync(ct);
        var waiting = await questions.ListByStatusAsync(QuestionStatus.AwaitingOwner, query.Take, ct);
        var views = await ReviewViews.BuildAsync(waiting, skills, forms, courses, ct);

        var items = views
            .Select(v => new OwnerQueueItem(
                v.Id, v.Version, v.ItemType, v.Skills.Select(s => s.Code).ToList(), v.Source,
                v.AiReview?.Verdict, v.AiReview?.AnswerKeyAgrees,
                v.AiReview?.Findings.Count(f => f.Severity == FindingSeverity.Problem) ?? 0,
                v.AiReview?.Findings.Count(f => f.Severity == FindingSeverity.Warning) ?? 0,
                v.AiReview?.Summary, v.Difficulty, v.AiReview?.SuggestedDifficulty))
            .OrderBy(i => i.Verdict == AiVerdict.NeedsAttention ? 0 : 1)
            .ThenBy(i => i.Source?.Exam).ThenBy(i => i.Source?.Year).ThenBy(i => i.Source?.ItemNumber)
            .ToList();

        return new OwnerQueueResponse(counts, items);
    }
}
