using K12.Domain.Content;
using K12.Domain.Curriculum;
using Microsoft.EntityFrameworkCore;

namespace K12.Infrastructure.Persistence;

public sealed class EfExamFormRepository(K12DbContext db) : IExamFormRepository
{
    public Task<ExamForm?> FindAsync(CourseId courseId, string exam, int year, string season, CancellationToken ct = default)
    {
        var e = exam.Trim();
        var s = season.Trim().ToLowerInvariant();
        return db.ExamForms.FirstOrDefaultAsync(f => f.CourseId == courseId && f.Exam == e && f.Year == year && f.Season == s, ct);
    }

    public async Task<IReadOnlyList<ExamForm>> ListContainingAsync(
        IReadOnlyCollection<QuestionId> questionIds, CancellationToken ct = default) =>
        await db.ExamForms.Where(f => f.Items.Any(i => questionIds.Contains(i.QuestionId))).ToListAsync(ct);

    public Task<ExamForm?> GetAsync(ExamFormId id, CancellationToken ct = default) =>
        db.ExamForms.FirstOrDefaultAsync(f => f.Id == id, ct);

    public async Task<IReadOnlyList<ExamForm>> ListByCourseAsync(CourseId courseId, CancellationToken ct = default) =>
        await db.ExamForms.Where(f => f.CourseId == courseId).OrderByDescending(f => f.Year).ThenBy(f => f.Season).ToListAsync(ct);

    public async Task AddAsync(ExamForm form, CancellationToken ct = default) => await db.ExamForms.AddAsync(form, ct);
}
