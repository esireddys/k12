using K12.Application.Abstractions;
using K12.Domain.Accounts;
using K12.Domain.Content;
using K12.Domain.Curriculum;
using K12.Domain.Progress;

namespace K12.Application.Progress;

/// <summary>The parent dashboard for one kid. Parents only, behind the parent PIN.</summary>
/// <param name="CourseId">
/// Another subject of the kid's grade to show (Reading next to Math). Empty, or a course that is not a subject of
/// the kid's grade, shows the subject the kid has open.
/// </param>
public sealed record GetKidDashboardQuery(Guid AccountId, Guid KidId, DateOnly? LocalDate, Guid? CourseId = null);

/// <param name="Accuracy">Share of all answers on the skill that were right, 0 to 100; empty before any answer.</param>
/// <param name="QuestionCount">Live questions on the skill.</param>
/// <param name="OpenMistakes">Questions on the skill the kid missed and has not answered right since.</param>
public sealed record SkillStanding(
    string Code, string Description, int? Category, MasteryLevel Level, int Answered, int Correct, int? Accuracy, bool HasQuestions,
    int QuestionCount = 0, int OpenMistakes = 0);

/// <param name="Minutes">Whole minutes, rounded. <paramref name="Seconds"/> says how much when it is under one.</param>
public sealed record WeekView(int DaysPracticed, int Minutes, int Answered, int Correct, int Seconds = 0);

/// <summary>The latest full released test or practice test, with the one before it to compare.</summary>
public sealed record ExamResultView(string Title, int Percent, int? PreviousPercent, DateTimeOffset FinishedAt);

/// <param name="FrameworkName">The course's standards framework, from curriculum data.</param>
/// <param name="Categories">Progress by reporting category, the way the state test reports results.</param>
/// <param name="NeedsAttention">
/// Skills to work on next: the ones with recent misses still to fix, newest first, then started ones with low
/// accuracy. Never a skill the kid has not started.
/// </param>
/// <param name="Skills">Skills with enough questions to judge on their own (see <see cref="GetKidDashboardHandler.SkillRowMinQuestions"/>).</param>
/// <param name="SkillsCountedInCategoriesOnly">Skills with too few questions for a row of their own; they still count in their category.</param>
public sealed record KidDashboard(
    Guid KidId, string Name, AvatarColor Avatar, Guid? CourseId, string? CourseName, string? FrameworkName,
    int Streak, int BestStreak, WeekView Week, int Answered, int Correct, int MistakesToFix,
    ExamResultView? LastExam, int CategoriesMastered, IReadOnlyList<CategoryView> Categories,
    IReadOnlyList<SkillStanding> NeedsAttention, IReadOnlyList<SkillStanding> Skills, int SkillsCountedInCategoriesOnly,
    IReadOnlyList<SessionView> RecentSessions);

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

    /// <summary>
    /// A skill gets its own row only with at least this many questions; fewer cannot show a trend (most released
    /// skills have only a few items). Every answer still counts toward its category.
    /// </summary>
    public const int SkillRowMinQuestions = 5;

    public async Task<Result<KidDashboard>> HandleAsync(GetKidDashboardQuery query, CancellationToken ct = default)
    {
        var family = await KidAccess.LoadAsync(accounts, query.AccountId, query.KidId, ct);
        if (!family.IsSuccess) return family.Error!;
        var kid = family.Value!.Kid;
        var today = KidAccess.Today(query.LocalDate, clock);
        var kidProgress = await progress.GetAsync(kid.Id, ct) ?? KidProgress.Start(kid.Id);

        var course = await ShownCourseAsync(kid.CourseId, query.CourseId, ct);
        var courseSkills = course is null ? [] : await skills.ListByCourseAsync(course.Id, ct);
        var live = course is null ? [] : CategoryViews.LiveIn(await questions.ListByStatusAsync(QuestionStatus.Live, int.MaxValue, ct), courseSkills);
        var questionsPerSkill = live.SelectMany(q => q.SkillIds).GroupBy(id => id).ToDictionary(g => g.Key, g => g.Count());

        var toFix = await Mistakes.OpenInCourseAsync(kidProgress, course?.Id, questions, skills, ct);
        var lastMiss = kidProgress.OpenMistakes.ToDictionary(m => m.QuestionId, m => m.LastMissedAt);
        var courseSkillIds = courseSkills.Select(s => s.Id).ToHashSet();
        var misses = toFix
            .Select(q => (Skill: KidAccess.SkillIn(q.SkillIds, courseSkillIds), At: lastMiss[q.Id]))
            .Where(m => m.Skill is not null)
            .GroupBy(m => m.Skill!.Value)
            .ToDictionary(g => g.Key, g => (Count: g.Count(), Latest: g.Max(m => m.At)));

        var bySkill = kidProgress.Skills.ToDictionary(s => s.SkillId);
        var standings = courseSkills
            .OrderBy(s => s.CategoryNumber ?? int.MaxValue).ThenBy(s => s.Code, StringComparer.Ordinal)
            .Select(s =>
            {
                var p = bySkill.GetValueOrDefault(s.Id);
                var count = questionsPerSkill.GetValueOrDefault(s.Id);
                return (Id: s.Id, View: new SkillStanding(
                    s.Code, s.Description, s.CategoryNumber, p?.Level ?? MasteryLevel.NotStarted,
                    p?.Answered ?? 0, p?.Correct ?? 0, p is { Answered: > 0 } ? Percent(p.Correct, p.Answered) : null,
                    count > 0, count, misses.GetValueOrDefault(s.Id).Count));
            })
            .ToList();

        // Recent misses first: that is what the kid just struggled with, whatever the skill's size.
        var missed = standings.Where(s => misses.ContainsKey(s.Id)).OrderByDescending(s => misses[s.Id].Latest).Select(s => s.View);
        var weak = standings.Select(s => s.View)
            .Where(s => s.HasQuestions && s.OpenMistakes == 0 && s.Level != MasteryLevel.Mastered
                && s.Answered >= AttentionMinAnswers && s.Accuracy < AttentionBelowPercent)
            .OrderBy(s => s.Accuracy).ThenByDescending(s => s.Answered);
        var attention = missed.Concat(weak).Take(AttentionCount).ToList();

        var listed = standings.Select(s => s.View).Where(s => s.QuestionCount >= SkillRowMinQuestions).ToList();
        var practicable = standings.Count(s => s.View.HasQuestions);
        var categories = course is null ? [] : await CategoryViews.ForAsync(kid.Id, course, courseSkills, live, sessions, questions, ct);

        var week = kidProgress.DaysBetween(today.AddDays(-6), today);
        var recent = (await sessions.ListRecentAsync(kid.Id, RecentCount * 3, ct))
            .Where(s => course is null || s.CourseId == course.Id).Take(RecentCount).ToList();
        var exams = recent.Where(s => s.Mode == PracticeMode.Exam && s.IsFinished && s.Total > 0).ToList();
        var lastExam = exams.Count == 0 ? null : new ExamResultView(
            exams[0].Title, Percent(exams[0].Correct, exams[0].Total),
            exams.Count > 1 ? Percent(exams[1].Correct, exams[1].Total) : null, exams[0].FinishedAt!.Value);

        return new KidDashboard(
            kid.Id.Value, kid.Name, kid.Avatar, course?.Id.Value, course?.Name, course?.FrameworkName,
            kidProgress.CurrentStreak(today), kidProgress.BestStreak(),
            new WeekView(week.Count(d => d.Answered > 0), (int)Math.Round(week.Sum(d => d.Seconds) / 60.0),
                week.Sum(d => d.Answered), week.Sum(d => d.Correct), week.Sum(d => d.Seconds)),
            kidProgress.TotalAnswered, kidProgress.TotalCorrect, toFix.Count,
            lastExam, categories.Count(c => c.Level == MasteryLevel.Mastered), categories,
            attention, listed, practicable - listed.Count, recent.Select(Sessions.View).ToList());
    }

    /// <summary>The subject asked for when it is another subject of the kid's grade, else the kid's own.</summary>
    private async Task<Course?> ShownCourseAsync(CourseId? kidCourse, Guid? asked, CancellationToken ct)
    {
        var ids = new[] { kidCourse, asked is { } a ? new CourseId(a) : null }.OfType<CourseId>().Distinct().ToList();
        if (ids.Count == 0) return null;
        var found = (await courses.ListByIdsAsync(ids, ct)).ToDictionary(c => c.Id);
        var own = kidCourse is { } k ? found.GetValueOrDefault(k) : null;
        var other = asked is { } x ? found.GetValueOrDefault(new CourseId(x)) : null;
        return other is not null && (own is null || other.IsSameLevelAs(own)) ? other : own;
    }

    private static int Percent(int part, int whole) => whole == 0 ? 0 : (int)Math.Round(100.0 * part / whole);
}
