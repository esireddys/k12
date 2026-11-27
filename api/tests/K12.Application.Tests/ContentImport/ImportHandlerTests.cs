using K12.Application.Abstractions;
using K12.Application.ContentImport.ImportCourse;
using K12.Application.ContentImport.ImportExamForm;
using K12.Application.ContentImport.RefreshExamForm;
using K12.Domain.Content;
using K12.Domain.Content.Answers;
using K12.Domain.Curriculum;

namespace K12.Application.Tests.ContentImport;

public class ImportHandlerTests
{
    private sealed class Store : ICourseRepository, ISkillRepository, IQuestionRepository, IExamFormRepository, IPassageRepository, IUnitOfWork, IClock
    {
        public List<Passage> Passages { get; } = [];
        public Task<Passage?> GetAsync(PassageId id, CancellationToken ct = default) => Task.FromResult(Passages.FirstOrDefault(p => p.Id == id));
        public Task<IReadOnlyList<Passage>> ListByIdsAsync(IReadOnlyCollection<PassageId> ids, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Passage>>(Passages.Where(p => ids.Contains(p.Id)).ToList());
        public Task AddAsync(Passage passage, CancellationToken ct = default) { Passages.Add(passage); return Task.CompletedTask; }
        public List<Course> Courses { get; } = [];
        public List<Skill> Skills { get; } = [];
        public List<Question> Questions { get; } = [];
        public List<ExamForm> Forms { get; } = [];
        public int Saves { get; private set; }
        public DateTimeOffset UtcNow { get; } = new(2026, 11, 27, 5, 0, 0, TimeSpan.Zero);

        public Task<Course?> FindAsync(string j, string s, string c, string language = Course.DefaultLanguage, CancellationToken ct = default) =>
            Task.FromResult(Courses.FirstOrDefault(x =>
                x.Jurisdiction == j.ToUpperInvariant() && x.Subject == s.ToLowerInvariant() && x.Code == c.ToUpperInvariant()
                && x.Language == language));
        public Task AddAsync(Course course, CancellationToken ct = default) { Courses.Add(course); return Task.CompletedTask; }
        public Task<IReadOnlyList<Skill>> ListByCourseAsync(CourseId id, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Skill>>(Skills.Where(s => s.CourseId == id).ToList());
        public Task AddAsync(Skill skill, CancellationToken ct = default) { Skills.Add(skill); return Task.CompletedTask; }
        public Task<Question?> GetAsync(QuestionId id, CancellationToken ct = default) => Task.FromResult(Questions.FirstOrDefault(q => q.Id == id));
        public Task AddAsync(Question question, CancellationToken ct = default) { Questions.Add(question); return Task.CompletedTask; }
        public Task<ExamForm?> FindAsync(CourseId id, string exam, int year, string season, CancellationToken ct = default) =>
            Task.FromResult(Forms.FirstOrDefault(f => f.CourseId == id && f.Exam == exam && f.Year == year && f.Season == season));
        public Task AddAsync(ExamForm form, CancellationToken ct = default) { Forms.Add(form); return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken ct = default) { Saves++; return Task.CompletedTask; }
        public Task<IReadOnlyList<Course>> ListByIdsAsync(IReadOnlyCollection<CourseId> ids, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Course>>(Courses.Where(c => ids.Contains(c.Id)).ToList());
        public Task<IReadOnlyList<Skill>> ListByIdsAsync(IReadOnlyCollection<SkillId> ids, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Skill>>(Skills.Where(s => ids.Contains(s.Id)).ToList());
        public Task<IReadOnlyList<Question>> ListByOriginAsync(
            QuestionOrigin origin, IReadOnlyCollection<QuestionStatus> statuses, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Question>>(Questions.Where(q => q.Origin == origin && statuses.Contains(q.Status)).ToList());
        public Task<IReadOnlyList<Question>> ListByStatusAsync(QuestionStatus status, int take, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Question>>(Questions.Where(q => q.Status == status).Take(take).ToList());
        public Task<IReadOnlyDictionary<QuestionStatus, int>> CountByStatusAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<QuestionStatus, int>>(Questions.GroupBy(q => q.Status).ToDictionary(g => g.Key, g => g.Count()));
        public Task<IReadOnlyList<ExamForm>> ListContainingAsync(IReadOnlyCollection<QuestionId> ids, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ExamForm>>(Forms.Where(f => f.Items.Any(i => ids.Contains(i.QuestionId))).ToList());
        public Task<IReadOnlyList<Course>> ListAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Course>>(Courses.ToList());
        public Task<ExamForm?> GetAsync(ExamFormId id, CancellationToken ct = default) => Task.FromResult(Forms.FirstOrDefault(f => f.Id == id));
        Task<IReadOnlyList<ExamForm>> IExamFormRepository.ListByCourseAsync(CourseId id, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ExamForm>>(Forms.Where(f => f.CourseId == id).ToList());
        public Task<IReadOnlyList<Question>> ListByIdsAsync(IReadOnlyCollection<QuestionId> ids, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Question>>(Questions.Where(q => ids.Contains(q.Id)).ToList());
    }

    private static ImportCourseCommand CourseCommand(params ImportSkill[] skills) => new(
        "TX", "math", "G3", "Grade 3", "FW", "Framework",
        [new ImportCategory(1, "Numbers"), new ImportCategory(2, "Computation")],
        skills.Length > 0 ? skills : [new ImportSkill("3.2(D)", "Compare", 1, "readiness", null), new ImportSkill("3.4(K)", "Solve", 2, "readiness", null)]);

    private static ImportCourseHandler CourseHandler(Store s) => new(s, s, s);
    private static ImportExamFormHandler FormHandler(Store s) => new(s, s, s, s, s, s, s);

    private static ImportItem Item(int n, string skill = "3.4(K)", string answer = "B") =>
        new(n, [skill], "choice", """{"stem":"?"}""", new AnswerSpec { Kind = AnswerKind.Choice, Value = answer });

    private static ImportExamFormCommand FormCommand(params ImportItem[] items) =>
        new("tx", "MATH", "g3", "EXAM", 2021, "spring", "https://example.org/t.pdf", items);

    [Fact]
    public async Task Importing_a_course_twice_updates_skills_instead_of_duplicating_them()
    {
        var store = new Store();
        var first = await CourseHandler(store).HandleAsync(CourseCommand());
        var second = await CourseHandler(store).HandleAsync(CourseCommand(
            new ImportSkill("3.2(D)", "Compare numbers", 1, "readiness", null),
            new ImportSkill("3.4(A)", "Add", 2, "readiness", null)));

        Assert.Equal((true, 2, 0), (first.Value!.CourseCreated, first.Value.SkillsCreated, first.Value.SkillsUpdated));
        Assert.Equal((false, 1, 1), (second.Value!.CourseCreated, second.Value.SkillsCreated, second.Value.SkillsUpdated));
        Assert.Single(store.Courses);
        Assert.Equal(3, store.Skills.Count);
        Assert.Equal("Compare numbers", store.Skills.Single(s => s.Code == "3.2(D)").Description);
    }

    [Theory]
    [InlineData("course", 9, CourseKind.Course, 9)]
    [InlineData("Grade", 3, CourseKind.Grade, 3)]
    [InlineData(null, 3, CourseKind.Grade, 3)]
    public async Task A_course_import_says_whether_it_is_a_grade_or_a_course(string? kind, int grade, CourseKind expected, int expectedGrade)
    {
        var store = new Store();
        var result = await CourseHandler(store).HandleAsync(CourseCommand() with { Code = "A1", Name = "Algebra I", Kind = kind, TypicalGrade = grade });

        Assert.True(result.IsSuccess);
        Assert.Equal((expected, (int?)expectedGrade), (store.Courses.Single().Kind, store.Courses.Single().TypicalGrade));
    }

    [Fact]
    public async Task Reimporting_a_course_without_a_kind_keeps_the_one_it_has()
    {
        var store = new Store();
        await CourseHandler(store).HandleAsync(CourseCommand() with { Kind = "course", TypicalGrade = 9 });
        await CourseHandler(store).HandleAsync(CourseCommand());
        Assert.Equal((CourseKind.Course, (int?)9), (store.Courses.Single().Kind, store.Courses.Single().TypicalGrade));
    }

    [Theory]
    [InlineData("semester", 9)]
    [InlineData("5", 9)]
    [InlineData("course", 14)]
    [InlineData("grade", null)]
    public async Task A_course_placed_outside_school_is_a_validation_error(string kind, int? grade)
    {
        var store = new Store();
        var result = await CourseHandler(store).HandleAsync(CourseCommand() with { Kind = kind, TypicalGrade = grade });
        Assert.Equal(("import.invalid_course", 0), (result.Error!.Code, store.Saves));
    }

    [Fact]
    public async Task A_skill_in_an_unknown_category_is_a_validation_error()
    {
        var store = new Store();
        var result = await CourseHandler(store).HandleAsync(CourseCommand(new ImportSkill("3.9(A)", "Income", 7, null, null)));
        Assert.Equal(ErrorKind.Validation, result.Error!.Kind);
        Assert.Equal(0, store.Saves);
    }

    [Fact]
    public async Task Duplicate_skill_codes_are_rejected()
    {
        var result = await CourseHandler(new Store()).HandleAsync(CourseCommand(
            new ImportSkill("3.4(K)", "a", 2, null, null), new ImportSkill("3.4(k)", "b", 2, null, null)));
        Assert.Equal("import.duplicate_skill", result.Error!.Code);
    }

    [Fact]
    public async Task An_exam_form_becomes_official_questions_that_are_live_without_review()
    {
        var store = new Store();
        await CourseHandler(store).HandleAsync(CourseCommand());

        var result = await FormHandler(store).HandleAsync(FormCommand(Item(2), Item(1, "3.2(D)", "C")));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.QuestionsCreated);
        Assert.All(store.Questions, q =>
        {
            Assert.Equal(QuestionOrigin.Official, q.Origin);
            Assert.Equal(QuestionStatus.Live, q.Status);
            Assert.True(q.IsVisibleToStudents);
            Assert.Null(q.AiReview);
            Assert.Null(q.OwnerDecision);
            var published = Assert.IsType<ReleasedQuestionPublished>(Assert.Single(q.DomainEvents));
            Assert.Equal(store.UtcNow, published.OccurredAt);
        });
        var form = Assert.Single(store.Forms);
        Assert.Equal([1, 2], form.Items.Select(i => i.Number));
        var first = store.Questions.Single(q => q.Id == form.Items[0].QuestionId);
        Assert.Equal([store.Skills.Single(s => s.Code == "3.2(D)").Id], first.SkillIds);
    }

    [Fact]
    public async Task The_same_form_is_imported_once()
    {
        var store = new Store();
        await CourseHandler(store).HandleAsync(CourseCommand());
        await FormHandler(store).HandleAsync(FormCommand(Item(1)));

        var again = await FormHandler(store).HandleAsync(FormCommand(Item(1)));

        Assert.Equal(ErrorKind.Conflict, again.Error!.Kind);
        Assert.Single(store.Questions);
    }

    [Fact]
    public async Task A_form_needs_its_course_first()
    {
        var result = await FormHandler(new Store()).HandleAsync(FormCommand(Item(1)));
        Assert.Equal(ErrorKind.NotFound, result.Error!.Kind);
    }

    [Theory]
    [InlineData("3.9(Z)", 1, "import.unknown_skill")]
    [InlineData("3.4(K)", 1, "import.duplicate_item")]
    public async Task Bad_items_are_rejected_before_anything_is_saved(string skill, int secondNumber, string code)
    {
        var store = new Store();
        await CourseHandler(store).HandleAsync(CourseCommand());
        var saves = store.Saves;

        var result = await FormHandler(store).HandleAsync(FormCommand(Item(1), Item(secondNumber == 1 && skill != "3.4(K)" ? 2 : secondNumber, skill)));

        Assert.Equal(code, result.Error!.Code);
        Assert.Empty(store.Questions);
        Assert.Equal(saves, store.Saves);
    }

    private static ImportItem ItemWith(int n, string content, string answer = "B") =>
        new(n, ["3.4(K)"], "choice", content, new AnswerSpec { Kind = AnswerKind.Choice, Value = answer });

    [Fact]
    public async Task Refreshing_a_form_updates_untouched_questions_and_keeps_ids_status_and_owner_edits()
    {
        var store = new Store();
        await CourseHandler(store).HandleAsync(CourseCommand());
        await FormHandler(store).HandleAsync(FormCommand(Item(1), Item(2)));
        var ids = store.Questions.Select(q => q.Id).ToList();
        store.Questions[1].Revise("""{"stem":"fixed by an owner"}""", new AnswerSpec { Kind = AnswerKind.Choice, Value = "B" });

        const string better = """{"stem":"?","choicesAreText":true}""";
        var handler = new RefreshExamFormHandler(store, store, store, store);
        var result = await handler.HandleAsync(new RefreshExamFormCommand(FormCommand(ItemWith(1, better), ItemWith(2, better))));

        Assert.True(result.IsSuccess);
        Assert.Equal((1, 1), (result.Value!.QuestionsRefreshed, result.Value.QuestionsKeptAsEdited));
        Assert.Equal(ids, store.Questions.Select(q => q.Id).ToList());
        Assert.Equal(better, store.Questions[0].ContentJson);
        Assert.Equal("""{"stem":"fixed by an owner"}""", store.Questions[1].ContentJson);
        Assert.All(store.Questions, q => Assert.Equal(QuestionStatus.Live, q.Status));

        var again = await handler.HandleAsync(new RefreshExamFormCommand(FormCommand(ItemWith(1, better), ItemWith(2, better))));
        Assert.Equal(0, again.Value!.QuestionsRefreshed);
    }

    public static TheoryData<string, ImportItem[], string> RefusedRefreshes => new()
    {
        { "a changed key", [ItemWith(1, "{}", answer: "C"), ItemWith(2, "{}")], "import.invalid_item" },
        { "an item more", [Item(1), Item(2), Item(3)], "import.items_differ" },
    };

    [Theory]
    [MemberData(nameof(RefusedRefreshes))]
    public async Task A_refresh_cannot_change_keys_or_items(string _, ImportItem[] items, string code)
    {
        var store = new Store();
        await CourseHandler(store).HandleAsync(CourseCommand());
        await FormHandler(store).HandleAsync(FormCommand(Item(1), Item(2)));
        var saves = store.Saves;

        var result = await new RefreshExamFormHandler(store, store, store, store).HandleAsync(new RefreshExamFormCommand(FormCommand(items)));

        Assert.Equal(code, result.Error!.Code);
        Assert.Equal(saves, store.Saves);
    }

    [Fact]
    public async Task Refreshing_a_form_that_was_never_imported_says_to_import_it()
    {
        var store = new Store();
        await CourseHandler(store).HandleAsync(CourseCommand());
        var result = await new RefreshExamFormHandler(store, store, store, store).HandleAsync(new RefreshExamFormCommand(FormCommand(Item(1))));
        Assert.Equal("import.form_not_found", result.Error!.Code);
    }
}
