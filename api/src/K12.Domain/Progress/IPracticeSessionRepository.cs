using K12.Domain.Accounts;

namespace K12.Domain.Progress;

/// <summary>Loads and saves <see cref="PracticeSession"/>s, a kid's attempt history. Implemented in Infrastructure.</summary>
public interface IPracticeSessionRepository
{
    Task<PracticeSession?> GetAsync(PracticeSessionId id, CancellationToken ct = default);

    /// <summary>A kid's latest sessions with at least one answer, newest first.</summary>
    Task<IReadOnlyList<PracticeSession>> ListRecentAsync(KidProfileId kidId, int take, CancellationToken ct = default);

    Task AddAsync(PracticeSession session, CancellationToken ct = default);

    /// <summary>Forgets every session of a kid, when the parent removes the kid.</summary>
    Task RemoveAllForKidAsync(KidProfileId kidId, CancellationToken ct = default);
}
