using K12.Application.Abstractions;
using K12.Domain.Accounts;
using K12.Domain.Content;
using K12.Domain.Curriculum;
using K12.Domain.Progress;

namespace K12.Application.Progress;

/// <summary>The parent dashboard for one kid. Parents only, behind the parent PIN.</summary>
public sealed record GetKidDashboardQuery(Guid AccountId, Guid KidId, DateOnly? LocalDate);

/// <param name="Accuracy">Share of all answers on the skill that were right, 0 to 100; empty before any answer.</param>
public sealed record SkillStanding(
    string Code, string Description, int? Category, MasteryLevel Level, int Answered, int Correct, int? Accuracy, bool HasQuestions);

public sealed record WeekView(int DaysPracticed, int Minutes, int Answered, int Correct);

/// <summary>The latest full released test or practice test, with the one before it to compare.</summary>
public sealed record ExamResultView(string Title, int Percent, int? PreviousPercent, DateTimeOffset FinishedAt);

/// <param name="FrameworkName">The course's standards framework, from curriculum data, for "9 of 24 skills".</param>
/// <param name="NeedsAttention">Skills to work on next: the weakest started ones, then ones not started yet.</param>
public sealed record KidDashboard(
    Guid KidId, string Name, AvatarColor Avatar, Guid? CourseId, string? CourseName, string? FrameworkName,
    int Streak, int BestStreak, WeekView Week, int Answered, int Correct, int MistakesToFix,
    ExamResultView? LastExam, int SkillsMastered, int SkillsTotal,
    IReadOnlyList<SkillStanding> NeedsAttention, IReadOnlyList<SkillStanding> Skills, IReadOnlyList<SessionView> RecentSessions);

public sealed class GetKidDashboardHandler(
    IParentAccountRepository accounts, IKidProgressRepository progress, IPracticeSessionRepository sessions,
    ICourseRepository courses, ISkillRepository skills, IQuestionRepository questions, IClock clock)
    : IQueryHandler<GetKidDashboardQuery, KidDashboard>
{
    public const int RecentCount = 10;
    public const int AttentionCount = 3;

    /// <summary>A started skill needs attention below this accuracy, once it has enough answers to judge.</summary>
    public const int AttentionBelowPercent = 70;
    public const int AttentionMinAnswers = 3;

    public async Task<Result<KidDashboard>> HandleAsync(GetKidDashboardQuery query, CancellationToken ct = default)
    {
        var family = await KidAccess.LoadAsync(accounts, query.AccountId, query.KidId, ct);
        if (!family.IsSuccess) return family.Error!;
        var kid = family.Value!.Kid;
        var today = KidAccess.Today(query.LocalDate, clock);
        var kidProgress = await progress.GetAsync(kid.Id, ct) ?? KidProgress.Start(kid.Id);

        var course = kid.CourseId is { } cid ? (await courses.ListByIdsAsync([cid], ct)).FirstOrDefault() : null;
        var courseSkills = course is null ? [] : await skills.ListByCourseAsync(course.Id, ct);
        var live = course is null ? [] : await questions.ListByStatusAsync(QuestionStatus.Live, int.MaxValue, ct);
        var skillsWithQuestions = live.Where(q => q.IsVisibleToStudents).SelectMany(q => q.SkillIds).ToHashSet();

        var bySkill = kidProgress.Skills.ToDictionary(s => s.SkillId);
        var standings = courseSkills
            .OrderBy(s => s.CategoryNumber ?? int.MaxValue).ThenBy(s => s.Code, StringComparer.Ordinal)
            .Select(s =>
            {
                var p = bySkill.GetValueOrDefault(s.Id);
                return new SkillStanding(
                    s.Code, s.Description, s.CategoryNumber, p?.Level ?? MasteryLevel.NotStarted,
                    p?.Answered ?? 0, p?.Correct ?? 0, p is { Answered: > 0 } ? Percent(p.Correct, p.Answered) : null,
                    skillsWithQuestions.Contains(s.Id));
            })
            .ToList();

        var practicable = standings.Where(s => s.HasQuestions).ToList();
        var weak = practicable
            .Where(s => s.Level != MasteryLevel.Mastered && s.Answered >= AttentionMinAnswers && s.Accuracy < AttentionBelowPercent)
            .OrderBy(s => s.Accuracy).ThenByDescending(s => s.Answered);
        var notStarted = practicable.Where(s => s.Level == MasteryLevel.NotStarted);
        var attention = weak.Concat(notStarted).Take(AttentionCount).ToList();

        var week = kidProgress.DaysBetween(today.AddDays(-6), today);
        var recent = await sessions.ListRecentAsync(kid.Id, RecentCount, ct);
        var exams = recent.Where(s => s.Mode == PracticeMode.Exam && s.IsFinished && s.Total > 0).ToList();
        var lastExam = exams.Count == 0 ? null : new ExamResultView(
            exams[0].Title, Percent(exams[0].Correct, exams[0].Total),
            exams.Count > 1 ? Percent(exams[1].Correct, exams[1].Total) : null, exams[0].FinishedAt!.Value);

        var toFix = await Mistakes.OpenInCourseAsync(kidProgress, kid.CourseId, questions, skills, ct);

        return new KidDashboard(
            kid.Id.Value, kid.Name, kid.Avatar, course?.Id.Value, course?.Name, course?.FrameworkName,
            kidProgress.CurrentStreak(today), kidProgress.BestStreak(),
            new WeekView(week.Count(d => d.Answered > 0), (int)Math.Round(week.Sum(d => d.Seconds) / 60.0),
                week.Sum(d => d.Answered), week.Sum(d => d.Correct)),
            kidProgress.TotalAnswered, kidProgress.TotalCorrect, toFix.Count,
            lastExam, practicable.Count(s => s.Level == MasteryLevel.Mastered), practicable.Count,
            attention, standings, recent.Select(Sessions.View).ToList());
    }

    private static int Percent(int part, int whole) => whole == 0 ? 0 : (int)Math.Round(100.0 * part / whole);
}
