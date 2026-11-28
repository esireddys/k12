namespace K12.Domain.Content;

/// <summary>Loads and saves <see cref="Question"/> aggregates. Implemented in Infrastructure.</summary>
public interface IQuestionRepository
{
    Task<Question?> GetAsync(QuestionId id, CancellationToken ct = default);
    Task<IReadOnlyList<Question>> ListByIdsAsync(IReadOnlyCollection<QuestionId> ids, CancellationToken ct = default);
    Task AddAsync(Question question, CancellationToken ct = default);

    /// <summary>Questions in one review state, at most <paramref name="take"/> of them.</summary>
    Task<IReadOnlyList<Question>> ListByStatusAsync(QuestionStatus status, int take, CancellationToken ct = default);

    /// <summary>Every question of one origin that is in any of <paramref name="statuses"/>.</summary>
    Task<IReadOnlyList<Question>> ListByOriginAsync(
        QuestionOrigin origin, IReadOnlyCollection<QuestionStatus> statuses, CancellationToken ct = default);

    /// <summary>How many questions are in each state. States with none are left out.</summary>
    Task<IReadOnlyDictionary<QuestionStatus, int>> CountByStatusAsync(CancellationToken ct = default);
}
