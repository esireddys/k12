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

    public async Task AddAsync(ExamForm form, CancellationToken ct = default) => await db.ExamForms.AddAsync(form, ct);
}
