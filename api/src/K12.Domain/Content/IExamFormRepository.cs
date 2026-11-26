using K12.Domain.Curriculum;

namespace K12.Domain.Content;

/// <summary>Loads and saves <see cref="ExamForm"/> aggregates.</summary>
public interface IExamFormRepository
{
    Task<ExamForm?> FindAsync(CourseId courseId, string exam, int year, string season, CancellationToken ct = default);
    Task AddAsync(ExamForm form, CancellationToken ct = default);
}
