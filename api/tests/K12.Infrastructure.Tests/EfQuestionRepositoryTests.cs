using K12.Domain.Content;
using K12.Domain.Content.Answers;
using K12.Domain.Curriculum;
using K12.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K12.Infrastructure.Tests;

/// <summary>
/// Round-trips aggregates through the real EF Core mappings. Uses SQLite in memory so CI needs no server;
/// the SQL Server migration is checked separately when it runs against a real database.
/// </summary>
public sealed class EfQuestionRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public EfQuestionRepositoryTests()
    {
        _connection.Open();
        using var db = NewContext();
        db.Database.EnsureCreated();
    }

    private K12DbContext NewContext() =>
        new(new DbContextOptionsBuilder<K12DbContext>().UseSqlite(_connection).Options);

    [Fact]
    public async Task A_saved_question_loads_back_with_its_skills_answer_and_status()
    {
        var skill = SkillId.New();
        var question = Question.Draft(
            [skill], "numeric", """{"stem":"3/4?"}""",
            new AnswerSpec { Kind = AnswerKind.Fraction, Value = "3/4", RequireSimplest = true, Accept = ["0.75"] },
            Difficulty.Harder, QuestionOrigin.Generated);
        question.SubmitForAiReview();
        question.PassAiReview();
        question.Approve("owner", DateTimeOffset.UnixEpoch);

        await using (var db = NewContext())
        {
            await new EfQuestionRepository(db).AddAsync(question);
            await db.SaveChangesAsync();
        }

        await using (var db = NewContext())
        {
            var loaded = await new EfQuestionRepository(db).GetAsync(question.Id);

            Assert.NotNull(loaded);
            Assert.Equal([skill], loaded.SkillIds);
            Assert.Equal(QuestionStatus.Live, loaded.Status);
            Assert.Equal(Difficulty.Harder, loaded.Difficulty);
            Assert.Equal(AnswerKind.Fraction, loaded.Answer.Kind);
            Assert.True(loaded.Answer.RequireSimplest);
            Assert.Equal(["0.75"], loaded.Answer.Accept);
            Assert.True(loaded.Check("0.75").IsCorrect);
            Assert.Empty(loaded.DomainEvents);
        }
    }

    [Fact]
    public async Task Unknown_ids_return_null()
    {
        await using var db = NewContext();
        Assert.Null(await new EfQuestionRepository(db).GetAsync(QuestionId.New()));
    }

    public void Dispose() => _connection.Dispose();
}
