using K12.Domain.Accounts;
using Microsoft.EntityFrameworkCore;

namespace K12.Infrastructure.Persistence;

public sealed class EfParentAccountRepository(K12DbContext db) : IParentAccountRepository
{
    public Task<ParentAccount?> GetAsync(ParentAccountId id, CancellationToken ct = default) =>
        db.ParentAccounts.FirstOrDefaultAsync(a => a.Id == id, ct);

    public Task<ParentAccount?> FindByLoginAsync(ExternalLogin login, CancellationToken ct = default) =>
        db.ParentAccounts.FirstOrDefaultAsync(a => a.Login.Provider == login.Provider && a.Login.Subject == login.Subject, ct);

    public async Task AddAsync(ParentAccount account, CancellationToken ct = default) => await db.ParentAccounts.AddAsync(account, ct);
}
