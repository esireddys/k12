using K12.Application.Abstractions;
using K12.Application.ContentReview;
using K12.Domain.Accounts;
using K12.Domain.Content;
using K12.Domain.Curriculum;

namespace K12.Application.Tests;

/// <summary>Every repository in memory, for handler tests that span aggregates.</summary>
public sealed class TestStore
    : ICourseRepository, ISkillRepository, IQuestionRepository, IExamFormRepository, IParentAccountRepository, IUnitOfWork, IClock, IItemImageStore
{
    public List<Course> Courses { get; } = [];
    public List<Skill> Skills { get; } = [];
    public List<Question> Questions { get; } = [];
    public List<ExamForm> Forms { get; } = [];
    public List<ParentAccount> Accounts { get; } = [];
    public Dictionary<string, byte[]> Images { get; } = [];
    public int Saves { get; private set; }
    public DateTimeOffset UtcNow { get; set; } = new(2026, 11, 27, 5, 0, 0, TimeSpan.Zero);

    public Task SaveChangesAsync(CancellationToken ct = default) { Saves++; return Task.CompletedTask; }

    // Courses
    public Task<Course?> FindAsync(string j, string s, string c, CancellationToken ct = default) =>
        Task.FromResult(Courses.FirstOrDefault(x => x.Jurisdiction == j && x.Subject == s && x.Code == c));
    public Task<IReadOnlyList<Course>> ListByIdsAsync(IReadOnlyCollection<CourseId> ids, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Course>>(Courses.Where(c => ids.Contains(c.Id)).ToList());
    public Task<IReadOnlyList<Course>> ListAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Course>>(Courses.ToList());
    public Task AddAsync(Course course, CancellationToken ct = default) { Courses.Add(course); return Task.CompletedTask; }

    // Skills
    Task<IReadOnlyList<Skill>> ISkillRepository.ListByCourseAsync(CourseId id, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Skill>>(Skills.Where(s => s.CourseId == id).ToList());
    public Task<IReadOnlyList<Skill>> ListByIdsAsync(IReadOnlyCollection<SkillId> ids, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Skill>>(Skills.Where(s => ids.Contains(s.Id)).ToList());
    public Task AddAsync(Skill skill, CancellationToken ct = default) { Skills.Add(skill); return Task.CompletedTask; }

    // Questions
    public Task<Question?> GetAsync(QuestionId id, CancellationToken ct = default) => Task.FromResult(Questions.FirstOrDefault(q => q.Id == id));
    public Task<IReadOnlyList<Question>> ListByIdsAsync(IReadOnlyCollection<QuestionId> ids, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Question>>(Questions.Where(q => ids.Contains(q.Id)).ToList());
    public Task AddAsync(Question question, CancellationToken ct = default) { Questions.Add(question); return Task.CompletedTask; }
    public Task<IReadOnlyList<Question>> ListByStatusAsync(QuestionStatus status, int take, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Question>>(Questions.Where(q => q.Status == status).Take(take).ToList());
    public Task<IReadOnlyList<Question>> ListByOriginAsync(
        QuestionOrigin origin, IReadOnlyCollection<QuestionStatus> statuses, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Question>>(Questions.Where(q => q.Origin == origin && statuses.Contains(q.Status)).ToList());
    public Task<IReadOnlyDictionary<QuestionStatus, int>> CountByStatusAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyDictionary<QuestionStatus, int>>(Questions.GroupBy(q => q.Status).ToDictionary(g => g.Key, g => g.Count()));

    // Exam forms
    public Task<ExamForm?> FindAsync(CourseId id, string exam, int year, string season, CancellationToken ct = default) =>
        Task.FromResult(Forms.FirstOrDefault(f => f.CourseId == id && f.Exam == exam && f.Year == year && f.Season == season));
    public Task<ExamForm?> GetAsync(ExamFormId id, CancellationToken ct = default) => Task.FromResult(Forms.FirstOrDefault(f => f.Id == id));
    Task<IReadOnlyList<ExamForm>> IExamFormRepository.ListByCourseAsync(CourseId id, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ExamForm>>(Forms.Where(f => f.CourseId == id).ToList());
    public Task<IReadOnlyList<ExamForm>> ListContainingAsync(IReadOnlyCollection<QuestionId> ids, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ExamForm>>(Forms.Where(f => f.Items.Any(i => ids.Contains(i.QuestionId))).ToList());
    public Task AddAsync(ExamForm form, CancellationToken ct = default) { Forms.Add(form); return Task.CompletedTask; }

    // Parent accounts
    public Task<ParentAccount?> GetAsync(ParentAccountId id, CancellationToken ct = default) => Task.FromResult(Accounts.FirstOrDefault(a => a.Id == id));
    public Task<ParentAccount?> FindByLoginAsync(ExternalLogin login, CancellationToken ct = default) =>
        Task.FromResult(Accounts.FirstOrDefault(a => a.Login == login));
    public Task AddAsync(ParentAccount account, CancellationToken ct = default) { Accounts.Add(account); return Task.CompletedTask; }

    // Pictures
    public Task<ItemImage?> OpenAsync(string relativePath, CancellationToken ct = default) =>
        Task.FromResult(Images.TryGetValue(relativePath, out var bytes) ? new ItemImage(new MemoryStream(bytes), "image/png") : null);
}
