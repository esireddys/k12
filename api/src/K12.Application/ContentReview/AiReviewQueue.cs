using K12.Application.Abstractions;
using K12.Domain.Content;
using K12.Domain.Curriculum;

namespace K12.Application.ContentReview;

public sealed record ListAiReviewQueueQuery(int Take = 50);

public sealed record AiReviewQueueResponse(IReadOnlyList<QuestionForReview> Items);

/// <summary>The questions waiting for the AI reviewer, with their skills and source, for the reviewer job to work through.</summary>
public sealed class ListAiReviewQueueHandler(
    IQuestionRepository questions, ISkillRepository skills, IExamFormRepository forms, ICourseRepository courses)
    : IQueryHandler<ListAiReviewQueueQuery, AiReviewQueueResponse>
{
    public async Task<Result<AiReviewQueueResponse>> HandleAsync(ListAiReviewQueueQuery query, CancellationToken ct = default)
    {
        if (query.Take is < 1 or > 500) return Error.Validation("review.take", "Take between 1 and 500 questions.");
        var waiting = await questions.ListByStatusAsync(QuestionStatus.InAiReview, query.Take, ct);
        var views = await ReviewViews.BuildAsync(waiting, skills, forms, courses, ct);
        return new AiReviewQueueResponse(views
            .OrderBy(v => v.Source?.Year).ThenBy(v => v.Source?.ItemNumber).ToList());
    }
}
