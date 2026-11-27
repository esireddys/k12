using K12.Domain.Content;
using Microsoft.EntityFrameworkCore;

namespace K12.Infrastructure.Persistence;

/// <summary>SQL Server implementation of <see cref="IQuestionRepository"/>. Saving is the unit of work's job.</summary>
public sealed class EfQuestionRepository(K12DbContext db) : IQuestionRepository
{
    public Task<Question?> GetAsync(QuestionId id, CancellationToken ct = default) =>
        db.Questions.FirstOrDefaultAsync(q => q.Id == id, ct);

    public async Task AddAsync(Question question, CancellationToken ct = default) =>
        await db.Questions.AddAsync(question, ct);

    public async Task<IReadOnlyList<Question>> ListByStatusAsync(QuestionStatus status, int take, CancellationToken ct = default) =>
        await db.Questions.Where(q => q.Status == status).OrderBy(q => q.Id).Take(take).ToListAsync(ct);

    public async Task<IReadOnlyList<Question>> ListByOriginAsync(
        QuestionOrigin origin, IReadOnlyCollection<QuestionStatus> statuses, CancellationToken ct = default) =>
        await db.Questions.Where(q => q.Origin == origin && statuses.Contains(q.Status)).OrderBy(q => q.Id).ToListAsync(ct);

    public async Task<IReadOnlyDictionary<QuestionStatus, int>> CountByStatusAsync(CancellationToken ct = default) =>
        await db.Questions.GroupBy(q => q.Status).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
}
