namespace K12.Domain.Curriculum;

/// <summary>Loads and saves <see cref="Course"/> aggregates.</summary>
public interface ICourseRepository
{
    Task<Course?> FindAsync(string jurisdiction, string subject, string code, CancellationToken ct = default);
    Task AddAsync(Course course, CancellationToken ct = default);
}
