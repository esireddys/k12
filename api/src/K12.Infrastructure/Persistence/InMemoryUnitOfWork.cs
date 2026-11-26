using K12.Application.Abstractions;

namespace K12.Infrastructure.Persistence;

/// <summary>Pairs with <see cref="InMemoryQuestionRepository"/>: changes are already applied.</summary>
public sealed class InMemoryUnitOfWork : IUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
}
