namespace K12.Domain.Content.Reports;

/// <summary>Loads and saves <see cref="QuestionReport"/> aggregates. Implemented in Infrastructure.</summary>
public interface IQuestionReportRepository
{
    Task<QuestionReport?> GetAsync(QuestionReportId id, CancellationToken ct = default);

    /// <summary>Reports no owner has closed yet, oldest first, at most <paramref name="take"/>.</summary>
    Task<IReadOnlyList<QuestionReport>> ListOpenAsync(int take, CancellationToken ct = default);

    /// <summary>Open reports on one question, for example to close them all once it is fixed.</summary>
    Task<IReadOnlyList<QuestionReport>> ListOpenForAsync(QuestionId questionId, CancellationToken ct = default);

    /// <summary>How many open reports each question has. Questions with none are left out.</summary>
    Task<IReadOnlyDictionary<QuestionId, int>> CountOpenByQuestionAsync(CancellationToken ct = default);

    Task AddAsync(QuestionReport report, CancellationToken ct = default);
}
