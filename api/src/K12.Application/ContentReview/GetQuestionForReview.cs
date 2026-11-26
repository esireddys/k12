using K12.Application.Abstractions;
using K12.Domain.Content;
using K12.Domain.Curriculum;

namespace K12.Application.ContentReview;

public sealed record GetQuestionForReviewQuery(Guid QuestionId);

/// <summary>One question in full for the owner's review screen, in any state, with the AI reviewer's findings.</summary>
public sealed class GetQuestionForReviewHandler(
    IQuestionRepository questions, ISkillRepository skills, IExamFormRepository forms, ICourseRepository courses)
    : IQueryHandler<GetQuestionForReviewQuery, QuestionForReview>
{
    public async Task<Result<QuestionForReview>> HandleAsync(GetQuestionForReviewQuery query, CancellationToken ct = default)
    {
        var question = await questions.GetAsync(new QuestionId(query.QuestionId), ct);
        if (question is null) return Error.NotFound("question.not_found", "No such question.");
        return (await ReviewViews.BuildAsync([question], skills, forms, courses, ct)).Single();
    }
}
