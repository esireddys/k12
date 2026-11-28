using System.Text.Json;
using K12.Application.Abstractions;
using K12.Domain.Common;
using K12.Domain.Content;
using K12.Domain.Content.Answers;
using K12.Domain.Content.Reports;
using K12.Domain.Curriculum;

namespace K12.Application.ContentReview;

/// <summary>
/// Every question in the bank an owner can open, for example to add an explanation or fix how an item was read.
/// Each filter is optional.
/// </summary>
/// <param name="Status">Live unless asked otherwise.</param>
/// <param name="HasExplanation">True for questions that have one, false for those still missing one.</param>
/// <param name="Reported">True for questions with open reports from families.</param>
public sealed record BrowseQuestionsQuery(
    Guid? CourseId = null, int? Year = null, string? SkillCode = null, bool? HasExplanation = null, bool? Reported = null,
    QuestionStatus Status = QuestionStatus.Live, int Take = 500);

public sealed record BrowsedQuestion(
    Guid Id, int Version, QuestionStatus Status, string ItemType, IReadOnlyList<string> SkillCodes, ReviewSource? Source,
    bool HasExplanation, int OpenReports);

public sealed record BrowseQuestionsResponse(int Total, IReadOnlyList<BrowsedQuestion> Items);

public sealed class BrowseQuestionsHandler(
    IQuestionRepository questions, ISkillRepository skills, IExamFormRepository forms, ICourseRepository courses,
    IQuestionReportRepository reports)
    : IQueryHandler<BrowseQuestionsQuery, BrowseQuestionsResponse>
{
    public async Task<Result<BrowseQuestionsResponse>> HandleAsync(BrowseQuestionsQuery query, CancellationToken ct = default)
    {
        if (query.Take is < 1 or > 2000) return Error.Validation("browse.take", "Take between 1 and 2000 questions.");
        IEnumerable<Question> found = await questions.ListByStatusAsync(query.Status, int.MaxValue, ct);

        if (query.CourseId is { } courseId)
        {
            var inCourse = (await skills.ListByCourseAsync(new CourseId(courseId), ct)).Select(s => s.Id).ToHashSet();
            found = found.Where(q => q.SkillIds.Any(inCourse.Contains));
        }

        var openReports = await reports.CountOpenByQuestionAsync(ct);
        if (query.Reported is { } reported) found = found.Where(q => openReports.ContainsKey(q.Id) == reported);
        if (query.HasExplanation is { } has) found = found.Where(q => HasExplanation(q.ContentJson) == has);

        var views = await ReviewViews.BuildAsync(found.ToList(), skills, forms, courses, ct);
        var items = views
            .Where(v => query.Year is not { } year || v.Source?.Year == year)
            .Where(v => string.IsNullOrWhiteSpace(query.SkillCode)
                || v.Skills.Any(s => string.Equals(s.Code, query.SkillCode.Trim(), StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(v => openReports.GetValueOrDefault(new QuestionId(v.Id)))
            .ThenBy(v => v.Source?.CourseCode).ThenBy(v => v.Source?.Subject).ThenByDescending(v => v.Source?.Year)
            .ThenBy(v => v.Source?.ItemNumber)
            .Select(v => new BrowsedQuestion(
                v.Id, v.Version, v.Status, v.ItemType, v.Skills.Select(s => s.Code).ToList(), v.Source,
                HasExplanation(v.Content), openReports.GetValueOrDefault(new QuestionId(v.Id))))
            .ToList();
        return new BrowseQuestionsResponse(items.Count, items.Take(query.Take).ToList());
    }

    private static bool HasExplanation(string json) => HasExplanation(ReviewViews.Json(json));

    private static bool HasExplanation(JsonElement content) =>
        content.ValueKind == JsonValueKind.Object && content.TryGetProperty("explanation", out var e)
        && e.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(e.GetString());
}

/// <summary>
/// An owner corrects a question students can already see: its text, choices, explanation or key. A released
/// question stays Live as a new version; a generated one goes back through review. Open reports on it are closed.
/// </summary>
/// <param name="Version">The version the owner looked at, so two owners cannot overwrite each other.</param>
public sealed record ReviseLiveQuestionCommand(Guid QuestionId, int Version, string Owner, JsonElement Content, AnswerSpec? Answer);

public sealed class ReviseLiveQuestionHandler(
    IQuestionRepository questions, IQuestionReportRepository reports, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<ReviseLiveQuestionCommand, ReviewStepResponse>
{
    public async Task<Result<ReviewStepResponse>> HandleAsync(ReviseLiveQuestionCommand command, CancellationToken ct = default)
    {
        var question = await questions.GetAsync(new QuestionId(command.QuestionId), ct);
        if (question is null) return Error.NotFound("question.not_found", "No such question.");
        if (question.Version != command.Version)
            return Error.Conflict("review.stale", $"Someone changed this question (now version {question.Version}). Reload it.");
        if (question.Status != QuestionStatus.Live)
            return Error.Conflict("review.wrong_state", $"The question is {question.Status}; use the review queue for it.");
        if (string.IsNullOrWhiteSpace(command.Owner)) return Error.Validation("review.invalid", "A change must name the owner.");

        try
        {
            question.Revise(OwnerDecisionGuard.Reviewed(command.Content), command.Answer ?? question.Answer);
            foreach (var report in await reports.ListOpenForAsync(question.Id, ct)) report.Close(command.Owner, clock.UtcNow);
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
            return Error.Conflict("review.concurrent", "Someone else changed this question at the same time. Reload it.");
        }
        return new ReviewStepResponse(question.Id.Value, question.Version, question.Status);
    }
}
