namespace K12.Application.Abstractions;

/// <summary>The current time, injectable so tests can control it.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

/// <summary>Commits the changes from one use case together.</summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct = default);
}
