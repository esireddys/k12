using K12.Domain.Content;
using Microsoft.EntityFrameworkCore;

namespace K12.Infrastructure.Persistence;

public sealed class EfPassageRepository(K12DbContext db) : IPassageRepository
{
    public Task<Passage?> GetAsync(PassageId id, CancellationToken ct = default) =>
        db.Passages.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<Passage>> ListByIdsAsync(IReadOnlyCollection<PassageId> ids, CancellationToken ct = default) =>
        await db.Passages.Where(p => ids.Contains(p.Id)).ToListAsync(ct);

    public async Task AddAsync(Passage passage, CancellationToken ct = default) => await db.Passages.AddAsync(passage, ct);
}
