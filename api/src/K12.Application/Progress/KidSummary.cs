using K12.Application.Abstractions;
using K12.Application.Practice.Sets;
using K12.Domain.Accounts;
using K12.Domain.Content;
using K12.Domain.Curriculum;
using K12.Domain.Progress;

namespace K12.Application.Progress;

/// <summary>What a kid's home screen shows about them.</summary>
public sealed record GetKidSummaryQuery(Guid AccountId, Guid KidId, DateOnly? LocalDate);

/// <param name="Stars">One per right answer, ever.</param>
/// <param name="MistakesToFix">Missed questions in the kid's course still waiting to be answered right.</param>
public sealed record KidSummary(Guid KidId, int Streak, int BestStreak, int Stars, int Answered, int MistakesToFix);

/// <summary>A set of the kid's missed questions in their course, oldest miss first.</summary>
public sealed record GetMistakesSetQuery(Guid AccountId, Guid KidId, int? Count);

public sealed class GetKidSummaryHandler(
    IParentAccountRepository accounts, IKidProgressRepository progress, IQuestionRepository questions, ISkillRepository skills, IClock clock)
    : IQueryHandler<GetKidSummaryQuery, KidSummary>
{
    public async Task<Result<KidSummary>> HandleAsync(GetKidSummaryQuery query, CancellationToken ct = default)
    {
        var family = await KidAccess.LoadAsync(accounts, query.AccountId, query.KidId, ct);
        if (!family.IsSuccess) return family.Error!;
        var kid = family.Value!.Kid;
        var kidProgress = await progress.GetAsync(kid.Id, ct);
        if (kidProgress is null) return new KidSummary(kid.Id.Value, 0, 0, 0, 0, 0);

        var toFix = await Mistakes.OpenInCourseAsync(kidProgress, kid.CourseId, questions, skills, ct);
        return new KidSummary(
            kid.Id.Value, kidProgress.CurrentStreak(KidAccess.Today(query.LocalDate, clock)), kidProgress.BestStreak(),
            kidProgress.TotalCorrect, kidProgress.TotalAnswered, toFix.Count);
    }
}

public sealed class GetMistakesSetHandler(
    IParentAccountRepository accounts, IKidProgressRepository progress, IQuestionRepository questions,
    ISkillRepository skills, IExamFormRepository forms, IPassageRepository passages)
    : IQueryHandler<GetMistakesSetQuery, PracticeSet>
{
    public const string Title = "Fix my mistakes";

    public async Task<Result<PracticeSet>> HandleAsync(GetMistakesSetQuery query, CancellationToken ct = default)
    {
        var family = await KidAccess.LoadAsync(accounts, query.AccountId, query.KidId, ct);
        if (!family.IsSuccess) return family.Error!;
        var kid = family.Value!.Kid;
        if (kid.CourseId is not { } courseId) return KidAccess.NoCourse;

        var kidProgress = await progress.GetAsync(kid.Id, ct);
        var count = Math.Clamp(query.Count ?? GetPracticeSetHandler.DefaultCount, 1, GetPracticeSetHandler.MaxCount);
        var open = kidProgress is null ? [] : await Mistakes.OpenInCourseAsync(kidProgress, courseId, questions, skills, ct);
        var skillsById = (await skills.ListByCourseAsync(courseId, ct)).ToDictionary(s => s.Id);
        var sources = StudentQuestions.Sources(await forms.ListContainingAsync(open.Take(count).Select(q => q.Id).ToList(), ct));
        // Missed questions about the same passage come together, so the kid reads it once.
        var picked = SetPassages.InPrintedOrder(open.Take(count).ToList(), sources);
        return new PracticeSet(courseId.Value, SetSource.Mistakes, Title,
            picked.Select(q => StudentQuestions.View(q, skillsById, sources)).ToList(),
            await SetPassages.ForAsync(picked, passages, ct));
    }
}

internal static class Mistakes
{
    /// <summary>
    /// The open mistakes whose questions are still Live and belong to the course, oldest miss first.
    /// A question taken down after the kid missed it never comes back.
    /// </summary>
    public static async Task<IReadOnlyList<Question>> OpenInCourseAsync(
        KidProgress kidProgress, CourseId? courseId, IQuestionRepository questions, ISkillRepository skills, CancellationToken ct)
    {
        if (courseId is not { } id) return [];
        var open = kidProgress.OpenMistakes.OrderBy(m => m.LastMissedAt).ToList();
        if (open.Count == 0) return [];

        var courseSkills = (await skills.ListByCourseAsync(id, ct)).Select(s => s.Id).ToHashSet();
        var byId = (await questions.ListByIdsAsync(open.Select(m => m.QuestionId).ToList(), ct))
            .Where(q => q.IsVisibleToStudents && q.SkillIds.Any(courseSkills.Contains))
            .ToDictionary(q => q.Id);
        return open.Where(m => byId.ContainsKey(m.QuestionId)).Select(m => byId[m.QuestionId]).ToList();
    }
}
