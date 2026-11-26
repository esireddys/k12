using K12.Application.Abstractions;
using K12.Application.ContentImport.ImportCourse;
using K12.Application.ContentImport.ImportExamForm;
using K12.Domain.Content;
using K12.Domain.Content.Answers;
using K12.Domain.Curriculum;

namespace K12.Application.Tests.ContentImport;

public class ImportHandlerTests
{
    private sealed class Store : ICourseRepository, ISkillRepository, IQuestionRepository, IExamFormRepository, IUnitOfWork
    {
        public List<Course> Courses { get; } = [];
        public List<Skill> Skills { get; } = [];
        public List<Question> Questions { get; } = [];
        public List<ExamForm> Forms { get; } = [];
        public int Saves { get; private set; }

        public Task<Course?> FindAsync(string j, string s, string c, CancellationToken ct = default) =>
            Task.FromResult(Courses.FirstOrDefault(x =>
                x.Jurisdiction == j.ToUpperInvariant() && x.Subject == s.ToLowerInvariant() && x.Code == c.ToUpperInvariant()));
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
        public Task<IReadOnlyList<Question>> ListByStatusAsync(QuestionStatus status, int take, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Question>>(Questions.Where(q => q.Status == status).Take(take).ToList());
        public Task<IReadOnlyDictionary<QuestionStatus, int>> CountByStatusAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<QuestionStatus, int>>(Questions.GroupBy(q => q.Status).ToDictionary(g => g.Key, g => g.Count()));
        public Task<IReadOnlyList<ExamForm>> ListContainingAsync(IReadOnlyCollection<QuestionId> ids, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ExamForm>>(Forms.Where(f => f.Items.Any(i => ids.Contains(i.QuestionId))).ToList());
    }

    private static ImportCourseCommand CourseCommand(params ImportSkill[] skills) => new(
        "TX", "math", "G3", "Grade 3", "FW", "Framework",
        [new ImportCategory(1, "Numbers"), new ImportCategory(2, "Computation")],
        skills.Length > 0 ? skills : [new ImportSkill("3.2(D)", "Compare", 1, "readiness", null), new ImportSkill("3.4(K)", "Solve", 2, "readiness", null)]);

    private static ImportCourseHandler CourseHandler(Store s) => new(s, s, s);
    private static ImportExamFormHandler FormHandler(Store s) => new(s, s, s, s, s);

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
    public async Task An_exam_form_becomes_official_questions_waiting_for_ai_review()
    {
        var store = new Store();
        await CourseHandler(store).HandleAsync(CourseCommand());

        var result = await FormHandler(store).HandleAsync(FormCommand(Item(2), Item(1, "3.2(D)", "C")));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.QuestionsCreated);
        Assert.All(store.Questions, q =>
        {
            Assert.Equal(QuestionOrigin.Official, q.Origin);
            Assert.Equal(QuestionStatus.InAiReview, q.Status);
            Assert.False(q.IsVisibleToStudents);
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
}
