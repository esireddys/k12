using K12.Domain.Curriculum;
using Microsoft.EntityFrameworkCore;

namespace K12.Infrastructure.Persistence;

public sealed class EfCourseRepository(K12DbContext db) : ICourseRepository
{
    public Task<Course?> FindAsync(string jurisdiction, string subject, string code, CancellationToken ct = default)
    {
        var (j, s, c) = CourseKey.Normalize(jurisdiction, subject, code);
        return db.Courses.FirstOrDefaultAsync(x => x.Jurisdiction == j && x.Subject == s && x.Code == c, ct);
    }

    public async Task<IReadOnlyList<Course>> ListByIdsAsync(IReadOnlyCollection<CourseId> ids, CancellationToken ct = default) =>
        await db.Courses.Where(c => ids.Contains(c.Id)).ToListAsync(ct);

    public async Task AddAsync(Course course, CancellationToken ct = default) => await db.Courses.AddAsync(course, ct);
}

public sealed class EfSkillRepository(K12DbContext db) : ISkillRepository
{
    public async Task<IReadOnlyList<Skill>> ListByCourseAsync(CourseId courseId, CancellationToken ct = default) =>
        await db.Skills.Where(s => s.CourseId == courseId).ToListAsync(ct);

    public async Task<IReadOnlyList<Skill>> ListByIdsAsync(IReadOnlyCollection<SkillId> ids, CancellationToken ct = default) =>
        await db.Skills.Where(s => ids.Contains(s.Id)).ToListAsync(ct);

    public async Task AddAsync(Skill skill, CancellationToken ct = default) => await db.Skills.AddAsync(skill, ct);
}

/// <summary>Matches how <see cref="Course"/> stores its key fields, so lookups are case-insensitive.</summary>
internal static class CourseKey
{
    public static (string Jurisdiction, string Subject, string Code) Normalize(string jurisdiction, string subject, string code) =>
        (jurisdiction.Trim().ToUpperInvariant(), subject.Trim().ToLowerInvariant(), code.Trim().ToUpperInvariant());
}
