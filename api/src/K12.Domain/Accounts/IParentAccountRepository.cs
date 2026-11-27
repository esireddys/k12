namespace K12.Domain.Accounts;

/// <summary>Loads and saves <see cref="ParentAccount"/> aggregates. Implemented in Infrastructure.</summary>
public interface IParentAccountRepository
{
    Task<ParentAccount?> GetAsync(ParentAccountId id, CancellationToken ct = default);
    Task<ParentAccount?> FindByLoginAsync(ExternalLogin login, CancellationToken ct = default);
    Task AddAsync(ParentAccount account, CancellationToken ct = default);
}
