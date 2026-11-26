using System.Collections.Concurrent;
using K12.Domain.Content;
using K12.Domain.Curriculum;

namespace K12.Infrastructure.Persistence;

/// <summary>In-memory courses for runs with no database. See <see cref="InMemoryQuestionRepository"/>.</summary>
public sealed class InMemoryCourseRepository : ICourseRepository
{
    private readonly ConcurrentDictionary<CourseId, Course> _store = new();

    public Task<Course?> FindAsync(string jurisdiction, string subject, string code, CancellationToken ct = default)
    {
        var (j, s, c) = CourseKey.Normalize(jurisdiction, subject, code);
        return Task.FromResult(_store.Values.FirstOrDefault(x => x.Jurisdiction == j && x.Subject == s && x.Code == c));
    }

    public Task<IReadOnlyList<Course>> ListByIdsAsync(IReadOnlyCollection<CourseId> ids, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Course>>(_store.Values.Where(c => ids.Contains(c.Id)).ToList());

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

    public Task AddAsync(ExamForm form, CancellationToken ct = default)
    {
        _store[form.Id] = form;
        return Task.CompletedTask;
    }
}
