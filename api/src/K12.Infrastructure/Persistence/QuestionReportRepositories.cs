using System.Collections.Concurrent;
using K12.Domain.Content;
using K12.Domain.Content.Reports;
using Microsoft.EntityFrameworkCore;

namespace K12.Infrastructure.Persistence;

public sealed class EfQuestionReportRepository(K12DbContext db) : IQuestionReportRepository
{
    public Task<QuestionReport?> GetAsync(QuestionReportId id, CancellationToken ct = default) =>
        db.QuestionReports.FirstOrDefaultAsync(r => r.Id == id, ct);

    // Open reports are few (owners close them), so they are ordered here rather than in SQL: the same code then
    // runs on SQLite in tests, which cannot ORDER BY a DateTimeOffset.
    public async Task<IReadOnlyList<QuestionReport>> ListOpenAsync(int take, CancellationToken ct = default) =>
        (await db.QuestionReports.Where(r => r.ClosedAt == null).ToListAsync(ct)).OrderBy(r => r.ReportedAt).Take(take).ToList();

    public async Task<IReadOnlyList<QuestionReport>> ListOpenForAsync(QuestionId questionId, CancellationToken ct = default) =>
        await db.QuestionReports.Where(r => r.QuestionId == questionId && r.ClosedAt == null).ToListAsync(ct);

    public async Task<IReadOnlyDictionary<QuestionId, int>> CountOpenByQuestionAsync(CancellationToken ct = default) =>
        await db.QuestionReports.Where(r => r.ClosedAt == null).GroupBy(r => r.QuestionId)
            .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);

    public async Task AddAsync(QuestionReport report, CancellationToken ct = default) => await db.QuestionReports.AddAsync(report, ct);
}

/// <summary>In-memory reports for runs with no database. See <see cref="InMemoryQuestionRepository"/>.</summary>
public sealed class InMemoryQuestionReportRepository : IQuestionReportRepository
{
    private readonly ConcurrentDictionary<QuestionReportId, QuestionReport> _store = new();

    public Task<QuestionReport?> GetAsync(QuestionReportId id, CancellationToken ct = default) => Task.FromResult(_store.GetValueOrDefault(id));

    public Task<IReadOnlyList<QuestionReport>> ListOpenAsync(int take, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<QuestionReport>>(_store.Values.Where(r => r.IsOpen).OrderBy(r => r.ReportedAt).Take(take).ToList());

    public Task<IReadOnlyList<QuestionReport>> ListOpenForAsync(QuestionId questionId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<QuestionReport>>(_store.Values.Where(r => r.IsOpen && r.QuestionId == questionId).ToList());

    public Task<IReadOnlyDictionary<QuestionId, int>> CountOpenByQuestionAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyDictionary<QuestionId, int>>(
            _store.Values.Where(r => r.IsOpen).GroupBy(r => r.QuestionId).ToDictionary(g => g.Key, g => g.Count()));

    public Task AddAsync(QuestionReport report, CancellationToken ct = default)
    {
        _store[report.Id] = report;
        return Task.CompletedTask;
    }
}
