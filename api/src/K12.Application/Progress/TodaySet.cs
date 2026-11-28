using K12.Application.Abstractions;
using K12.Application.Practice.Sets;
using K12.Domain.Accounts;
using K12.Domain.Content;
using K12.Domain.Curriculum;
using K12.Domain.Progress;

namespace K12.Application.Progress;

/// <summary>
/// "Today's practice" for a kid: one tap, about ten questions. A few of their open mistakes come first, most of the
/// rest from the part of the test they are weakest in, and the last few from anywhere in the course.
/// </summary>
/// <param name="Seed">Makes the set repeatable, so a refresh gives the same questions.</param>
public sealed record GetTodaySetQuery(Guid AccountId, Guid KidId, int? Count = null, int? Seed = null);

public sealed class GetTodaySetHandler(
    IParentAccountRepository accounts, IKidProgressRepository progress, IQuestionRepository questions,
    ISkillRepository skills, ICourseRepository courses, IExamFormRepository forms, IPassageRepository passages,
    IPracticeSessionRepository sessions)
    : IQueryHandler<GetTodaySetQuery, PracticeSet>
{
    public const string Title = GetPracticeSetHandler.TodayTitle;

    /// <summary>At most this many of the kid's open mistakes, so the set is never only old misses.</summary>
    public const int MaxMistakes = 3;

    /// <summary>The last questions of the set come from anywhere in the course, so nothing goes stale.</summary>
    public const int FromAnywhere = 2;

    public async Task<Result<PracticeSet>> HandleAsync(GetTodaySetQuery query, CancellationToken ct = default)
    {
        var family = await KidAccess.LoadAsync(accounts, query.AccountId, query.KidId, ct);
        if (!family.IsSuccess) return family.Error!;
        var kid = family.Value!.Kid;
        if (kid.CourseId is not { } courseId) return KidAccess.NoCourse;
        var course = (await courses.ListByIdsAsync([courseId], ct)).FirstOrDefault();
        if (course is null) return Error.NotFound("practice.no_course", "No such course.");

        var count = Math.Clamp(query.Count ?? GetPracticeSetHandler.DefaultCount, 1, GetPracticeSetHandler.MaxCount);
        var random = new Random(query.Seed ?? Random.Shared.Next());
        var courseSkills = await skills.ListByCourseAsync(courseId, ct);
        var skillsById = courseSkills.ToDictionary(s => s.Id);
        var live = CategoryViews.LiveIn(await questions.ListByStatusAsync(QuestionStatus.Live, int.MaxValue, ct), courseSkills);

        var kidProgress = await progress.GetAsync(kid.Id, ct);
        var mistakes = kidProgress is null ? [] : await Mistakes.OpenInCourseAsync(kidProgress, courseId, questions, skills, ct);
        var categories = await CategoryViews.ForAsync(kid.Id, course, courseSkills, live, sessions, questions, ct);

        var picked = Pick(count, mistakes, live, Weakest(categories), q => StudentQuestions.CategoryOf(q, skillsById), random);
        var sources = StudentQuestions.Sources(await forms.ListContainingAsync(picked.Select(q => q.Id).ToList(), ct));
        picked = SetPassages.InPrintedOrder(picked, sources);
        return new PracticeSet(courseId.Value, SetSource.Today, Title,
            picked.Select(q => StudentQuestions.View(q, skillsById, sources)).ToList(),
            await SetPassages.ForAsync(picked, passages, ct));
    }

    /// <summary>
    /// The category to work on: the started one with the lowest accuracy that is not mastered, else the one with the
    /// fewest answers. Only categories with questions count. Empty when there is nothing to choose.
    /// </summary>
    public static int? Weakest(IReadOnlyList<CategoryView> categories)
    {
        var withQuestions = categories.Where(c => c.QuestionCount > 0).ToList();
        var weak = withQuestions
            .Where(c => c.Answered > 0 && c.Level != MasteryLevel.Mastered)
            .OrderBy(c => c.Accuracy).ThenBy(c => c.Number)
            .FirstOrDefault();
        return (weak ?? withQuestions.OrderBy(c => c.Answered).ThenBy(c => c.Number).FirstOrDefault())?.Number;
    }

    /// <summary>
    /// Mistakes first (oldest miss first, at most <see cref="MaxMistakes"/>), then the weakest category, then anywhere.
    /// Reading questions come with the rest of their passage's questions, so a selection is read once.
    /// </summary>
    internal static List<Question> Pick(
        int count, IReadOnlyList<Question> mistakes, IReadOnlyList<Question> live, int? weakest,
        Func<Question, int?> categoryOf, Random random)
    {
        var picked = new List<Question>();
        var taken = new HashSet<QuestionId>();
        void Add(IEnumerable<Question> questions)
        {
            foreach (var q in questions)
                if (taken.Add(q.Id)) picked.Add(q);
        }

        Add(mistakes.Take(Math.Min(MaxMistakes, count)));

        // The other open mistakes wait for Fix my mistakes, so the set never turns into old misses only.
        var missed = mistakes.Select(q => q.Id).ToHashSet();
        var shuffled = GetPracticeSetHandler.Shuffle(live, random).Where(q => !missed.Contains(q.Id)).ToList();
        var focusTarget = Math.Max(0, count - FromAnywhere - picked.Count);
        if (weakest is { } category && focusTarget > 0)
            Add(PracticeTests.TakeGroups(shuffled.Where(q => categoryOf(q) == category).ToList(), focusTarget));

        var rest = count - picked.Count;
        if (rest > 0) Add(PracticeTests.TakeGroups(shuffled.Where(q => !taken.Contains(q.Id)).ToList(), rest));
        // A small bank: fill up with the remaining mistakes rather than come up short.
        foreach (var q in mistakes)
        {
            if (picked.Count >= count) break;
            if (taken.Add(q.Id)) picked.Add(q);
        }
        return SetPassages.GroupByPassage(picked);
    }
}
