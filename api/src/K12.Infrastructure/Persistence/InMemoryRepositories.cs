using System.Collections.Concurrent;
using K12.Domain.Accounts;
using K12.Domain.Content;
using K12.Domain.Curriculum;
using K12.Domain.Progress;

namespace K12.Infrastructure.Persistence;

/// <summary>In-memory courses for runs with no database. See <see cref="InMemoryQuestionRepository"/>.</summary>
public sealed class InMemoryCourseRepository : ICourseRepository
{
    private readonly ConcurrentDictionary<CourseId, Course> _store = new();

    public Task<Course?> FindAsync(
        string jurisdiction, string subject, string code, string language = Course.DefaultLanguage, CancellationToken ct = default)
    {
        var (j, s, c) = CourseKey.Normalize(jurisdiction, subject, code);
        var l = Course.NormalizeLanguage(language);
        return Task.FromResult(_store.Values.FirstOrDefault(x => x.Jurisdiction == j && x.Subject == s && x.Code == c && x.Language == l));
    }

    public Task<IReadOnlyList<Course>> ListByIdsAsync(IReadOnlyCollection<CourseId> ids, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Course>>(_store.Values.Where(c => ids.Contains(c.Id)).ToList());

    public Task<IReadOnlyList<Course>> ListAllAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Course>>(_store.Values.OrderBy(c => c.Jurisdiction).ThenBy(c => c.Subject).ThenBy(c => c.Code).ToList());

    public Task AddAsync(Course course, CancellationToken ct = default)
    {
        _store[course.Id] = course;
        return Task.CompletedTask;
    }
}

public sealed class InMemorySkillRepository : ISkillRepository
{
    private readonly ConcurrentDictionary<SkillId, Skill> _store = new();

    public Task<IReadOnlyList<Skill>> ListByCourseAsync(CourseId courseId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Skill>>(_store.Values.Where(s => s.CourseId == courseId).ToList());

    public Task<IReadOnlyList<Skill>> ListByIdsAsync(IReadOnlyCollection<SkillId> ids, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Skill>>(_store.Values.Where(s => ids.Contains(s.Id)).ToList());

    public Task AddAsync(Skill skill, CancellationToken ct = default)
    {
        _store[skill.Id] = skill;
        return Task.CompletedTask;
    }
}

public sealed class InMemoryExamFormRepository : IExamFormRepository
{
    private readonly ConcurrentDictionary<ExamFormId, ExamForm> _store = new();

    public Task<ExamForm?> FindAsync(CourseId courseId, string exam, int year, string season, CancellationToken ct = default) =>
        Task.FromResult(_store.Values.FirstOrDefault(f =>
            f.CourseId == courseId && f.Exam == exam.Trim() && f.Year == year && f.Season == season.Trim().ToLowerInvariant()));

    public Task<IReadOnlyList<ExamForm>> ListContainingAsync(
        IReadOnlyCollection<QuestionId> questionIds, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ExamForm>>(
            _store.Values.Where(f => f.Items.Any(i => questionIds.Contains(i.QuestionId))).ToList());

    public Task<ExamForm?> GetAsync(ExamFormId id, CancellationToken ct = default) => Task.FromResult(_store.GetValueOrDefault(id));

    public Task<IReadOnlyList<ExamForm>> ListByCourseAsync(CourseId courseId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ExamForm>>(
            _store.Values.Where(f => f.CourseId == courseId).OrderByDescending(f => f.Year).ThenBy(f => f.Season).ToList());

    public Task AddAsync(ExamForm form, CancellationToken ct = default)
    {
        _store[form.Id] = form;
        return Task.CompletedTask;
    }
}

public sealed class InMemoryPassageRepository : IPassageRepository
{
    private readonly ConcurrentDictionary<PassageId, Passage> _store = new();

    public Task<Passage?> GetAsync(PassageId id, CancellationToken ct = default) => Task.FromResult(_store.GetValueOrDefault(id));

    public Task<IReadOnlyList<Passage>> ListByIdsAsync(IReadOnlyCollection<PassageId> ids, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Passage>>(_store.Values.Where(p => ids.Contains(p.Id)).ToList());

    public Task AddAsync(Passage passage, CancellationToken ct = default)
    {
        _store[passage.Id] = passage;
        return Task.CompletedTask;
    }
}

public sealed class InMemoryParentAccountRepository : IParentAccountRepository
{
    private readonly ConcurrentDictionary<ParentAccountId, ParentAccount> _store = new();

    public Task<ParentAccount?> GetAsync(ParentAccountId id, CancellationToken ct = default) =>
        Task.FromResult(_store.GetValueOrDefault(id));

    public Task<ParentAccount?> FindByLoginAsync(ExternalLogin login, CancellationToken ct = default) =>
        Task.FromResult(_store.Values.FirstOrDefault(a => a.Login == login));

    public Task AddAsync(ParentAccount account, CancellationToken ct = default)
    {
        _store[account.Id] = account;
        return Task.CompletedTask;
    }
}

public sealed class InMemoryKidProgressRepository : IKidProgressRepository
{
    private readonly ConcurrentDictionary<KidProfileId, KidProgress> _store = new();

    public Task<KidProgress?> GetAsync(KidProfileId kidId, CancellationToken ct = default) => Task.FromResult(_store.GetValueOrDefault(kidId));

    public Task<IReadOnlyList<KidProgress>> ListAsync(IReadOnlyCollection<KidProfileId> kidIds, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<KidProgress>>(_store.Values.Where(p => kidIds.Contains(p.Id)).ToList());

    public Task AddAsync(KidProgress progress, CancellationToken ct = default)
    {
        _store[progress.Id] = progress;
        return Task.CompletedTask;
    }

    public void Remove(KidProgress progress) => _store.TryRemove(progress.Id, out _);
}

public sealed class InMemoryPracticeSessionRepository : IPracticeSessionRepository
{
    private readonly ConcurrentDictionary<PracticeSessionId, PracticeSession> _store = new();

    public Task<PracticeSession?> GetAsync(PracticeSessionId id, CancellationToken ct = default) => Task.FromResult(_store.GetValueOrDefault(id));

    public Task<IReadOnlyList<PracticeSession>> ListRecentAsync(KidProfileId kidId, int take, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<PracticeSession>>(_store.Values
            .Where(s => s.KidId == kidId && s.Answers.Count > 0).OrderByDescending(s => s.StartedAt).Take(take).ToList());

    public Task<IReadOnlyList<PracticeSession>> ListByCourseAsync(KidProfileId kidId, CourseId courseId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<PracticeSession>>(_store.Values.Where(s => s.KidId == kidId && s.CourseId == courseId).ToList());

    public Task AddAsync(PracticeSession session, CancellationToken ct = default)
    {
        _store[session.Id] = session;
        return Task.CompletedTask;
    }

    public Task RemoveAllForKidAsync(KidProfileId kidId, CancellationToken ct = default)
    {
        foreach (var session in _store.Values.Where(s => s.KidId == kidId).ToList()) _store.TryRemove(session.Id, out _);
        return Task.CompletedTask;
    }
}
