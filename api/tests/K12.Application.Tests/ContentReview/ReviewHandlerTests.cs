using System.Text.Json;
using K12.Application.Abstractions;
using K12.Application.ContentReview;
using K12.Domain.Content;
using K12.Domain.Content.Answers;
using K12.Domain.Content.Review;
using K12.Domain.Curriculum;

namespace K12.Application.Tests.ContentReview;

public class ReviewHandlerTests
{
    private sealed class Store : ICourseRepository, ISkillRepository, IQuestionRepository, IExamFormRepository, IUnitOfWork, IClock
    {
        public List<Course> Courses { get; } = [];
        public List<Skill> Skills { get; } = [];
        public List<Question> Questions { get; } = [];
        public List<ExamForm> Forms { get; } = [];
        public int Saves { get; private set; }
        public DateTimeOffset UtcNow => new(2026, 11, 26, 9, 0, 0, TimeSpan.Zero);

        public Task<Course?> FindAsync(string j, string s, string c, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Course>> ListByIdsAsync(IReadOnlyCollection<CourseId> ids, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Course>>(Courses.Where(c => ids.Contains(c.Id)).ToList());
        public Task AddAsync(Course course, CancellationToken ct = default) { Courses.Add(course); return Task.CompletedTask; }
        public Task<IReadOnlyList<Skill>> ListByCourseAsync(CourseId id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Skill>> ListByIdsAsync(IReadOnlyCollection<SkillId> ids, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Skill>>(Skills.Where(s => ids.Contains(s.Id)).ToList());
        public Task AddAsync(Skill skill, CancellationToken ct = default) { Skills.Add(skill); return Task.CompletedTask; }
        public Task<Question?> GetAsync(QuestionId id, CancellationToken ct = default) => Task.FromResult(Questions.FirstOrDefault(q => q.Id == id));
        public Task AddAsync(Question question, CancellationToken ct = default) { Questions.Add(question); return Task.CompletedTask; }
        public Task<IReadOnlyList<Question>> ListByOriginAsync(
            QuestionOrigin origin, IReadOnlyCollection<QuestionStatus> statuses, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Question>>(Questions.Where(q => q.Origin == origin && statuses.Contains(q.Status)).ToList());
        public Task<IReadOnlyList<Question>> ListByStatusAsync(QuestionStatus status, int take, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Question>>(Questions.Where(q => q.Status == status).Take(take).ToList());
        public Task<IReadOnlyDictionary<QuestionStatus, int>> CountByStatusAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<QuestionStatus, int>>(Questions.GroupBy(q => q.Status).ToDictionary(g => g.Key, g => g.Count()));
        public Task<ExamForm?> FindAsync(CourseId id, string exam, int year, string season, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ExamForm>> ListContainingAsync(IReadOnlyCollection<QuestionId> ids, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ExamForm>>(Forms.Where(f => f.Items.Any(i => ids.Contains(i.QuestionId))).ToList());
        public Task AddAsync(ExamForm form, CancellationToken ct = default) { Forms.Add(form); return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken ct = default) { Saves++; return Task.CompletedTask; }
    }

    /// <summary>A course with one skill and a three-item form, all in AI review.</summary>
    private static Store Seeded()
    {
        var s = new Store();
        var course = Course.Create("TX", "math", "G3", "Grade 3", "FW", "Framework", [new ReportingCategory(1, "Numbers")]);
        var skill = Skill.Define(course, "3.4(K)", "Solve", 1, "readiness", """{"maxDenominator":8}""");
        var form = ExamForm.Create(course.Id, "EXAM", 2022, "spring", "https://example.org/t.pdf");
        s.Courses.Add(course);
        s.Skills.Add(skill);
        for (var n = 1; n <= 3; n++)
        {
            var q = Question.Draft([skill.Id], "choice", $$"""{"stem":"item {{n}}","textNeedsReview":true}""",
                new AnswerSpec { Kind = AnswerKind.Choice, Value = "B" }, Difficulty.OnLevel, QuestionOrigin.Generated);
            q.SubmitForAiReview();
            form.AddItem(n, q.Id);
            s.Questions.Add(q);
        }
        s.Forms.Add(form);
        return s;
    }

    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static RecordAiReviewCommand Review(Question q, AiVerdict verdict = AiVerdict.LooksGood, int? version = null,
        string proposed = """{"stem":"clean","textNeedsReview":false}""") =>
        new(q.Id.Value, version ?? q.Version, "claude-test", verdict, verdict == AiVerdict.LooksGood, "B", Difficulty.Easier,
            Json(proposed), "Checked against the picture.",
            verdict == AiVerdict.LooksGood ? [] : [new AiFinding(FindingSeverity.Problem, "answer_key", "Key looks wrong.")]);

    private static RecordAiReviewHandler Record(Store s) => new(s, s, s);

    [Fact]
    public async Task The_ai_queue_carries_skills_and_where_each_item_came_from()
    {
        var s = Seeded();
        var result = await new ListAiReviewQueueHandler(s, s, s, s).HandleAsync(new ListAiReviewQueueQuery());

        Assert.Equal([1, 2, 3], result.Value!.Items.Select(i => i.Source!.ItemNumber));
        var first = result.Value.Items[0];
        Assert.Equal(("Grade 3", "EXAM", 2022), (first.Source!.CourseName, first.Source.Exam, first.Source.Year));
        Assert.Equal(8, first.Skills.Single().Limits.GetProperty("maxDenominator").GetInt32());
        Assert.Equal("item 1", first.Content.GetProperty("stem").GetString());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(501)]
    public async Task The_ai_queue_size_is_bounded(int take)
    {
        var s = Seeded();
        var result = await new ListAiReviewQueueHandler(s, s, s, s).HandleAsync(new ListAiReviewQueueQuery(take));
        Assert.Equal(ErrorKind.Validation, result.Error!.Kind);
    }

    [Fact]
    public async Task Recording_a_review_stores_it_with_the_server_clock_and_saves()
    {
        var s = Seeded();
        var q = s.Questions[0];
        var result = await Record(s).HandleAsync(Review(q));

        Assert.Equal(QuestionStatus.AwaitingOwner, result.Value!.Status);
        Assert.Equal(s.UtcNow, q.AiReview!.ReviewedAt);
        Assert.Equal(Difficulty.Easier, q.AiReview.SuggestedDifficulty);
        Assert.Equal(1, s.Saves);
    }

    public static TheoryData<string, ErrorKind> RecordFailures => new()
    {
        { "stale", ErrorKind.Conflict },
        { "twice", ErrorKind.Conflict },
        { "missing", ErrorKind.NotFound },
        { "array", ErrorKind.Validation },
        { "contradiction", ErrorKind.Validation },
    };

    [Theory]
    [MemberData(nameof(RecordFailures))]
    public async Task Bad_reviews_are_refused_without_saving(string scenario, ErrorKind kind)
    {
        var s = Seeded();
        var q = s.Questions[0];
        if (scenario == "twice") await Record(s).HandleAsync(Review(q));
        var saves = s.Saves;

        var command = scenario switch
        {
            "stale" => Review(q, version: 7),
            "missing" => Review(q) with { QuestionId = Guid.NewGuid() },
            "array" => Review(q, proposed: "[]"),
            "contradiction" => Review(q) with { AnswerKeyAgrees = false },
            _ => Review(q),
        };
        var result = await Record(s).HandleAsync(command);

        Assert.Equal(kind, result.Error!.Kind);
        Assert.Equal(saves, s.Saves);
    }

    [Fact]
    public async Task The_owner_queue_puts_flagged_items_first_and_counts_every_state()
    {
        var s = Seeded();
        await Record(s).HandleAsync(Review(s.Questions[0]));
        await Record(s).HandleAsync(Review(s.Questions[2], AiVerdict.NeedsAttention));

        var queue = (await new GetOwnerQueueHandler(s, s, s, s).HandleAsync(new GetOwnerQueueQuery())).Value!;

        Assert.Equal([3, 1], queue.Items.Select(i => i.Source!.ItemNumber));
        Assert.Equal((AiVerdict.NeedsAttention, 1), (queue.Items[0].Verdict!.Value, queue.Items[0].ProblemCount));
        Assert.Equal(2, queue.Counts[QuestionStatus.AwaitingOwner]);
        Assert.Equal(1, queue.Counts[QuestionStatus.InAiReview]);
    }

    [Fact]
    public async Task Approving_with_edits_drops_the_import_flag_and_goes_live()
    {
        var s = Seeded();
        var q = s.Questions[0];
        await Record(s).HandleAsync(Review(q));

        var result = await new ApproveQuestionHandler(s, s, s).HandleAsync(new ApproveQuestionCommand(
            q.Id.Value, q.Version, "owner@example.org", Json("""{"stem":"clean","textNeedsReview":true}"""),
            Difficulty: Difficulty.Easier));

        Assert.Equal((QuestionStatus.Live, 2), (result.Value!.Status, result.Value.Version));
        Assert.Equal("""{"stem":"clean"}""", q.ContentJson);
        Assert.Equal(Difficulty.Easier, q.Difficulty);
        Assert.Equal("B", q.Answer.Value);
        Assert.Equal("owner@example.org", q.OwnerDecision!.DecidedBy);
    }

    [Fact]
    public async Task Approving_without_edits_goes_live_as_it_stands()
    {
        var s = Seeded();
        var q = s.Questions[0];
        await Record(s).HandleAsync(Review(q));

        var result = await new ApproveQuestionHandler(s, s, s).HandleAsync(new ApproveQuestionCommand(q.Id.Value, 1, "owner"));

        Assert.Equal((QuestionStatus.Live, 1), (result.Value!.Status, result.Value.Version));
        Assert.Equal(OwnerOutcome.Approved, q.OwnerDecision!.Outcome);
    }

    [Fact]
    public async Task Owners_cannot_decide_on_a_stale_version_or_an_item_not_waiting_for_them()
    {
        var s = Seeded();
        var q = s.Questions[0];

        var notYet = await new ApproveQuestionHandler(s, s, s).HandleAsync(new ApproveQuestionCommand(q.Id.Value, 1, "owner"));
        await Record(s).HandleAsync(Review(q));
        var stale = await new RejectQuestionHandler(s, s, s).HandleAsync(new RejectQuestionCommand(q.Id.Value, 9, "owner", "x"));

        Assert.Equal(("review.wrong_state", "review.stale"), (notYet.Error!.Code, stale.Error!.Code));
        Assert.Equal(QuestionStatus.AwaitingOwner, q.Status);
    }

    [Fact]
    public async Task Rejecting_without_a_reason_is_a_validation_error()
    {
        var s = Seeded();
        var q = s.Questions[0];
        await Record(s).HandleAsync(Review(q));

        var result = await new RejectQuestionHandler(s, s, s).HandleAsync(new RejectQuestionCommand(q.Id.Value, 1, "owner", " "));

        Assert.Equal(ErrorKind.Validation, result.Error!.Kind);
        Assert.Equal(QuestionStatus.AwaitingOwner, q.Status);
    }

    [Fact]
    public async Task The_review_screen_shows_the_ai_proposal_next_to_the_original()
    {
        var s = Seeded();
        var q = s.Questions[1];
        await Record(s).HandleAsync(Review(q));

        var view = (await new GetQuestionForReviewHandler(s, s, s, s).HandleAsync(new GetQuestionForReviewQuery(q.Id.Value))).Value!;

        Assert.Equal("item 2", view.Content.GetProperty("stem").GetString());
        Assert.Equal("clean", view.AiReview!.ProposedContent.GetProperty("stem").GetString());
        Assert.Equal(2, view.Source!.ItemNumber);
    }
}
