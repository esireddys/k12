namespace K12.Domain.Content;

/// <summary>Loads and saves <see cref="Question"/> aggregates. Implemented in Infrastructure.</summary>
public interface IQuestionRepository
{
    Task<Question?> GetAsync(QuestionId id, CancellationToken ct = default);
    Task AddAsync(Question question, CancellationToken ct = default);
}
