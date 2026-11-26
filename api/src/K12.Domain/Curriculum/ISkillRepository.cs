namespace K12.Domain.Curriculum;

/// <summary>Loads and saves <see cref="Skill"/> aggregates.</summary>
public interface ISkillRepository
{
    Task<IReadOnlyList<Skill>> ListByCourseAsync(CourseId courseId, CancellationToken ct = default);
    Task<IReadOnlyList<Skill>> ListByIdsAsync(IReadOnlyCollection<SkillId> ids, CancellationToken ct = default);
    Task AddAsync(Skill skill, CancellationToken ct = default);
}
