using K12.Domain.Accounts;
using K12.Domain.Curriculum;
using K12.Domain.Progress;
using Microsoft.EntityFrameworkCore;

namespace K12.Infrastructure.Persistence;

public sealed class EfKidProgressRepository(K12DbContext db) : IKidProgressRepository
{
    public Task<KidProgress?> GetAsync(KidProfileId kidId, CancellationToken ct = default) =>
        db.KidProgress.AsSplitQuery().FirstOrDefaultAsync(p => p.Id == kidId, ct);

    public async Task<IReadOnlyList<KidProgress>> ListAsync(IReadOnlyCollection<KidProfileId> kidIds, CancellationToken ct = default) =>
        await db.KidProgress.AsSplitQuery().Where(p => kidIds.Contains(p.Id)).ToListAsync(ct);

    public async Task AddAsync(KidProgress progress, CancellationToken ct = default) => await db.KidProgress.AddAsync(progress, ct);

    public void Remove(KidProgress progress) => db.KidProgress.Remove(progress);
}

public sealed class EfPracticeSessionRepository(K12DbContext db) : IPracticeSessionRepository
{
    public Task<PracticeSession?> GetAsync(PracticeSessionId id, CancellationToken ct = default) =>
        db.PracticeSessions.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<IReadOnlyList<PracticeSession>> ListRecentAsync(KidProfileId kidId, int take, CancellationToken ct = default)
    {
        // Pick the ids from a light projection first, then load only those sessions with their answers.
        // Ordering happens here rather than in SQL so the same code runs on SQLite in tests (no DateTimeOffset ORDER BY there).
        var keys = await db.PracticeSessions
            .Where(s => s.KidId == kidId && s.Answers.Any())
            .Select(s => new { s.Id, s.StartedAt })
            .ToListAsync(ct);
        var ids = keys.OrderByDescending(k => k.StartedAt).Take(take).Select(k => k.Id).ToList();
        var sessions = await db.PracticeSessions.Where(s => ids.Contains(s.Id)).ToListAsync(ct);
        return sessions.OrderByDescending(s => s.StartedAt).ToList();
    }

    public async Task<IReadOnlyList<PracticeSession>> ListByCourseAsync(KidProfileId kidId, CourseId courseId, CancellationToken ct = default) =>
        await db.PracticeSessions.Where(s => s.KidId == kidId && s.CourseId == courseId).ToListAsync(ct);

    public async Task AddAsync(PracticeSession session, CancellationToken ct = default) => await db.PracticeSessions.AddAsync(session, ct);

    public async Task RemoveAllForKidAsync(KidProfileId kidId, CancellationToken ct = default) =>
        db.PracticeSessions.RemoveRange(await db.PracticeSessions.Where(s => s.KidId == kidId).ToListAsync(ct));
}
