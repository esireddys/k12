using System.Collections.Concurrent;
using K12.Domain.Content;

namespace K12.Infrastructure.Persistence;

/// <summary>
/// Stand-in until the EF Core + PostgreSQL repository lands. Same interface, so nothing above
/// Infrastructure changes when it is swapped.
/// </summary>
public sealed class InMemoryQuestionRepository : IQuestionRepository
{
    private readonly ConcurrentDictionary<QuestionId, Question> _store = new();

    public Task<Question?> GetAsync(QuestionId id, CancellationToken ct = default) =>
        Task.FromResult(_store.GetValueOrDefault(id));

    public Task AddAsync(Question question, CancellationToken ct = default)
    {
        _store[question.Id] = question;
        return Task.CompletedTask;
    }
}
