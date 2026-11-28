using K12.Domain.Accounts;

namespace K12.Domain.Progress;

/// <summary>Loads and saves <see cref="KidProgress"/>, one per kid. Implemented in Infrastructure.</summary>
public interface IKidProgressRepository
{
    Task<KidProgress?> GetAsync(KidProfileId kidId, CancellationToken ct = default);
    Task<IReadOnlyList<KidProgress>> ListAsync(IReadOnlyCollection<KidProfileId> kidIds, CancellationToken ct = default);
    Task AddAsync(KidProgress progress, CancellationToken ct = default);
    void Remove(KidProgress progress);
}
