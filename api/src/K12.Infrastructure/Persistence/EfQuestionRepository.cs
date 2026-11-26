using K12.Domain.Content;
using Microsoft.EntityFrameworkCore;

namespace K12.Infrastructure.Persistence;

/// <summary>SQL Server implementation of <see cref="IQuestionRepository"/>. Saving is the unit of work's job.</summary>
public sealed class EfQuestionRepository(K12DbContext db) : IQuestionRepository
{
    public Task<Question?> GetAsync(QuestionId id, CancellationToken ct = default) =>
        db.Questions.FirstOrDefaultAsync(q => q.Id == id, ct);

    public async Task AddAsync(Question question, CancellationToken ct = default) =>
        await db.Questions.AddAsync(question, ct);
}
