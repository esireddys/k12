namespace K12.Application.Abstractions;

/// <summary>The current time, injectable so tests can control it.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

/// <summary>Commits the changes from one use case together.</summary>
public interface IUnitOfWork
{
    /// <exception cref="ConcurrencyConflictException">Someone else saved the same aggregate first.</exception>
    Task SaveChangesAsync(CancellationToken ct = default);
}

/// <summary>Another request changed the aggregate between this use case loading and saving it.</summary>
public sealed class ConcurrencyConflictException(Exception inner)
    : Exception("Someone else changed this at the same time.", inner);

/// <summary>Hashes the parent PIN so only a slow, salted hash is stored. Implemented in Infrastructure.</summary>
public interface IPinHasher
{
    string Hash(string pin);
    bool Verify(string pin, string hash);
}
