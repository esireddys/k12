using K12.Domain.Curriculum;

namespace K12.Domain.Content;

/// <summary>Loads and saves <see cref="ExamForm"/> aggregates.</summary>
public interface IExamFormRepository
{
    Task<ExamForm?> FindAsync(CourseId courseId, string exam, int year, string season, CancellationToken ct = default);
    Task<ExamForm?> GetAsync(ExamFormId id, CancellationToken ct = default);

    /// <summary>Every released test of one course.</summary>
    Task<IReadOnlyList<ExamForm>> ListByCourseAsync(CourseId courseId, CancellationToken ct = default);

    Task AddAsync(ExamForm form, CancellationToken ct = default);

    /// <summary>The forms that hold any of these questions, so a reviewer can see where an item came from.</summary>
    Task<IReadOnlyList<ExamForm>> ListContainingAsync(IReadOnlyCollection<QuestionId> questionIds, CancellationToken ct = default);
}
