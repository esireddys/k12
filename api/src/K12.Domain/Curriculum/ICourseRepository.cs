namespace K12.Domain.Curriculum;

/// <summary>Loads and saves <see cref="Course"/> aggregates.</summary>
public interface ICourseRepository
{
    /// <summary>A course by its key. Every course is English unless <paramref name="language"/> says otherwise.</summary>
    Task<Course?> FindAsync(
        string jurisdiction, string subject, string code, string language = Course.DefaultLanguage, CancellationToken ct = default);
    Task<IReadOnlyList<Course>> ListByIdsAsync(IReadOnlyCollection<CourseId> ids, CancellationToken ct = default);
    Task<IReadOnlyList<Course>> ListAllAsync(CancellationToken ct = default);
    Task AddAsync(Course course, CancellationToken ct = default);
}
