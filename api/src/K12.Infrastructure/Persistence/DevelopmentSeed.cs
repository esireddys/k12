using K12.Application.Abstractions;
using K12.Domain.Content;
using K12.Domain.Content.Answers;
using K12.Domain.Curriculum;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace K12.Infrastructure.Persistence;

/// <summary>Development only: applies migrations and adds one made-up Live question (not a real released item).</summary>
public static class DevelopmentSeed
{
    public static async Task<QuestionId?> RunAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;

        var db = sp.GetService<K12DbContext>();
        if (db is not null)
        {
            await db.Database.MigrateAsync(ct);
            var existing = await db.Questions.Select(q => (QuestionId?)q.Id).FirstOrDefaultAsync(ct);
            if (existing is not null) return existing;
        }

        var question = Question.Draft(
            [SkillId.New()],
            itemType: "numeric",
            contentJson: """{"stem":"A school has 6 classes. Each class has 24 students. How many students are in the school?"}""",
            answer: new AnswerSpec { Kind = AnswerKind.Integer, Value = "144" },
            difficulty: Difficulty.OnLevel,
            origin: QuestionOrigin.Generated);
        question.SubmitForAiReview();
        question.PassAiReview();
        question.Approve("development-seed", sp.GetRequiredService<IClock>().UtcNow);
        question.ClearDomainEvents();

        await sp.GetRequiredService<IQuestionRepository>().AddAsync(question, ct);
        await sp.GetRequiredService<IUnitOfWork>().SaveChangesAsync(ct);
        return question.Id;
    }
}
