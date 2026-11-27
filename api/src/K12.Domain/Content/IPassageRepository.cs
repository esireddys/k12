namespace K12.Domain.Content;

/// <summary>Loads and saves <see cref="Passage"/> aggregates. Implemented in Infrastructure.</summary>
public interface IPassageRepository
{
    Task<Passage?> GetAsync(PassageId id, CancellationToken ct = default);
    Task<IReadOnlyList<Passage>> ListByIdsAsync(IReadOnlyCollection<PassageId> ids, CancellationToken ct = default);
    Task AddAsync(Passage passage, CancellationToken ct = default);
}
