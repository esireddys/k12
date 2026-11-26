using System.Collections.Concurrent;
using K12.Domain.Content;

namespace K12.Infrastructure.Persistence;

/// <summary>
/// Used when no SQL Server connection string is configured. Same interface as
/// <see cref="EfQuestionRepository"/>, so nothing above Infrastructure knows which one runs.
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

    public Task<IReadOnlyList<Question>> ListByStatusAsync(QuestionStatus status, int take, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Question>>(_store.Values.Where(q => q.Status == status).Take(take).ToList());

    public Task<IReadOnlyDictionary<QuestionStatus, int>> CountByStatusAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyDictionary<QuestionStatus, int>>(
            _store.Values.GroupBy(q => q.Status).ToDictionary(g => g.Key, g => g.Count()));
}
