using K12.Application.Practice.Sets;
using K12.Application.Progress;
using K12.Domain.Accounts;
using K12.Domain.Content;
using K12.Domain.Content.Answers;
using K12.Domain.Curriculum;
using K12.Domain.Progress;

namespace K12.Application.Tests.Progress;

public class TodayAndSubjectTests
{
    /// <summary>Grade 3 Math (6 questions in category 1, 6 in category 2) and Grade 3 Reading, one kid in Math.</summary>
    private sealed class Family
    {
        public TestStore S { get; } = new();
        public Course Math { get; }
        public Course Reading { get; }
        public List<Question> Numbers { get; } = [];
        public List<Question> Shapes { get; } = [];
        public ParentAccount Account { get; }
        public KidProfile Kid { get; }
        public DateOnly Today => DateOnly.FromDateTime(S.UtcNow.UtcDateTime);

        public Family()
        {
            Math = Course.Create("TX", "math", "G3", "Grade 3", "FW", "Framework",
                [new ReportingCategory(1, "Numerical Representations", "Numbers"), new ReportingCategory(2, "Geometry", "Shapes")]);
            Reading = Course.Create("TX", "reading", "G3", "Grade 3", "FW", "Framework", [new ReportingCategory(1, "Across genres")]);
            var numbers = Skill.Define(Math, "3.2(A)", "Place value", 1, null, "{}");
            var shapes = Skill.Define(Math, "3.6(A)", "Shapes", 2, null, "{}");
            var words = Skill.Define(Reading, "3.3(B)", "Word meaning", 1, null, "{}");
            S.Courses.AddRange([Math, Reading]);
            S.Skills.AddRange([numbers, shapes, words]);
            for (var n = 1; n <= 12; n++)
            {
                var q = Live(n <= 6 ? numbers : shapes, n);
                (n <= 6 ? Numbers : Shapes).Add(q);
            }
            Live(words, 99);

            Account = ParentAccount.Register(new ExternalLogin("google", "1"), "pat@example.org", "Pat", S.UtcNow);
            Kid = Account.AddKid("Maya", AvatarColor.Orange, Math.Id);
            S.Accounts.Add(Account);
        }

        private Question Live(Skill skill, int n)
        {
            var q = Question.Draft([skill.Id], "numeric", $$"""{"stem":"Item {{n}}"}""",
                new AnswerSpec { Kind = AnswerKind.Integer, Value = $"{n}" }, Difficulty.OnLevel, QuestionOrigin.Official);
            q.PublishReleased(S.UtcNow);
            S.Questions.Add(q);
            return q;
        }

        public Task Answer(PracticeMode mode, params (Question Q, string Response)[] answers) =>
            new RecordAnswersHandler(S, S, S, S, S, S, S).HandleAsync(new RecordAnswersCommand(
                Account.Id.Value, Kid.Id.Value, Guid.NewGuid(), mode, "Topic", "Set", Today, 20,
                answers.Select(a => new ResponseItem(a.Q.Id.Value, a.Response)).ToList(), true));

        public Task<Application.Abstractions.Result<PracticeSet>> TodayAsync(int count = 10) =>
            new GetTodaySetHandler(S, S, S, S, S, S, S, S).HandleAsync(new GetTodaySetQuery(Account.Id.Value, Kid.Id.Value, count, 7));

        public async Task<KidDashboard> DashboardAsync(Guid? courseId = null) =>
            (await new GetKidDashboardHandler(S, S, S, S, S, S, S)
                .HandleAsync(new GetKidDashboardQuery(Account.Id.Value, Kid.Id.Value, Today, courseId))).Value!;
    }

    [Fact]
    public async Task Today_starts_with_mistakes_then_the_weakest_category_then_anything()
    {
        var f = new Family();
        // Shapes is the weak spot: 4 misses there, all right in Numbers.
        await f.Answer(PracticeMode.Practice,
            (f.Shapes[0], "0"), (f.Shapes[1], "0"), (f.Shapes[2], "0"), (f.Shapes[3], "0"),
            (f.Numbers[0], "1"), (f.Numbers[1], "2"));

        var set = (await f.TodayAsync()).Value!;
        var ids = set.Questions.Select(q => q.Id).ToList();

        Assert.Equal((SetSource.Today, GetTodaySetHandler.Title, 10), (set.Source, set.Title, ids.Count));
        Assert.Equal(ids.Count, ids.Distinct().Count());
        // At most three old misses, oldest first, then the rest of the shapes questions.
        Assert.Equal(GetTodaySetHandler.MaxMistakes, ids.Count(id => f.Shapes.Take(4).Any(q => q.Id.Value == id)));
        Assert.All(f.Shapes.Skip(4), q => Assert.Contains(q.Id.Value, ids));
        Assert.DoesNotContain(set.Questions, q => q.SkillCode == "3.3(B)"); // never another subject
    }

    [Fact]
    public async Task Today_for_a_kid_who_has_not_started_is_a_mix_of_the_course()
    {
        var f = new Family();
        var set = (await f.TodayAsync(5)).Value!;
        Assert.Equal(5, set.Questions.Count);
        Assert.All(set.Questions, q => Assert.Contains(q.Category, new int?[] { 1, 2 }));
    }

    public static TheoryData<string, (int Number, MasteryLevel Level, int Answered, int? Accuracy, int Questions)[], int?> Weakest => new()
    {
        { "lowest accuracy among started", [(1, MasteryLevel.Learning, 4, 75, 5), (2, MasteryLevel.Learning, 4, 25, 5)], 2 },
        { "a mastered one is never picked", [(1, MasteryLevel.Mastered, 8, 50, 5), (2, MasteryLevel.NotStarted, 0, null, 5)], 2 },
        { "nothing started: the least practiced", [(1, MasteryLevel.NotStarted, 0, null, 5), (2, MasteryLevel.NotStarted, 0, null, 5)], 1 },
        { "categories with no questions are skipped", [(1, MasteryLevel.Learning, 2, 0, 0), (2, MasteryLevel.Learning, 2, 50, 3)], 2 },
        { "no categories", [], null },
    };

    [Theory]
    [MemberData(nameof(Weakest))]
    public void The_weakest_category_is_the_one_to_work_on(
        string _, (int Number, MasteryLevel Level, int Answered, int? Accuracy, int Questions)[] categories, int? expected)
    {
        var views = categories.Select(c => new CategoryView(c.Number, $"C{c.Number}", c.Level, c.Answered, 0, c.Accuracy, c.Questions)).ToList();
        Assert.Equal(expected, GetTodaySetHandler.Weakest(views));
    }

    [Fact]
    public async Task The_dashboard_can_show_another_subject_of_the_kids_grade()
    {
        var f = new Family();
        await f.Answer(PracticeMode.Practice, (f.Numbers[0], "0"));

        var math = await f.DashboardAsync();
        var reading = await f.DashboardAsync(f.Reading.Id.Value);
        Assert.Equal((f.Math.Id.Value, 1, 1), (math.CourseId!.Value, math.MistakesToFix, math.RecentSessions.Count));
        Assert.Equal((f.Reading.Id.Value, 0, 0), (reading.CourseId!.Value, reading.MistakesToFix, reading.RecentSessions.Count));
        Assert.Equal(["Across genres"], reading.Categories.Select(c => c.Name));
        // Answer totals follow the subject; the week is the kid's whole week.
        Assert.Equal((1, 0, 1), (math.Answered, reading.Answered, reading.Week.Answered));

        // A course of another grade is not one of the kid's subjects: their own shows instead.
        var other = Course.Create("TX", "math", "A1", "Algebra I", "FW", "Framework", [new ReportingCategory(1, "Numbers")]);
        f.S.Courses.Add(other);
        Assert.Equal(f.Math.Id.Value, (await f.DashboardAsync(other.Id.Value)).CourseId);
    }

    [Fact]
    public async Task A_short_week_counts_seconds_so_it_never_reads_as_no_practice()
    {
        var f = new Family();
        await f.Answer(PracticeMode.Practice, (f.Numbers[0], "1"));
        var week = (await f.DashboardAsync()).Week;
        Assert.Equal((0, 20), (week.Minutes, week.Seconds));
    }

    [Fact]
    public async Task Kids_see_the_short_category_names_and_parents_keep_the_frameworks()
    {
        var f = new Family();
        var summary = (await new GetKidSummaryHandler(f.S, f.S, f.S, f.S, f.S, f.S, f.S)
            .HandleAsync(new GetKidSummaryQuery(f.Account.Id.Value, f.Kid.Id.Value, f.Today))).Value!;
        Assert.Equal([("Numerical Representations", "Numbers"), ("Geometry", "Shapes")], summary.Categories.Select(c => (c.Name, c.KidName)));

        var catalog = (await new GetPracticeCatalogHandler(f.S, f.S, f.S, f.S).HandleAsync(new GetPracticeCatalogQuery())).Value!;
        var reading = catalog.Courses.Single(c => c.Subject == "reading");
        Assert.Equal("Across genres", reading.Categories.Single().KidName); // no short name: the framework's
    }
}
