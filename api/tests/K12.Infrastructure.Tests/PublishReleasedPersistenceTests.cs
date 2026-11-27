using K12.Application.Abstractions;
using K12.Application.ContentImport.PublishReleased;
using K12.Domain.Content;
using K12.Domain.Content.Answers;
using K12.Domain.Curriculum;
using K12.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K12.Infrastructure.Tests;

/// <summary>
/// Imports made before released questions skipped review left them in AI review or waiting for an owner.
/// Publishing moves exactly those Live and leaves everything else as it was.
/// </summary>
public sealed class PublishReleasedPersistenceTests : IDisposable
{
    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 11, 27, 5, 0, 0, TimeSpan.Zero);
    }

    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public PublishReleasedPersistenceTests()
    {
        _connection.Open();
        using var db = NewContext();
        db.Database.EnsureCreated();
    }

    private K12DbContext NewContext() =>
        new(new DbContextOptionsBuilder<K12DbContext>().UseSqlite(_connection).Options);

    private static Question New(QuestionOrigin origin) => Question.Draft(
        [SkillId.New()], "choice", """{"stem":"?"}""", new AnswerSpec { Kind = AnswerKind.Choice, Value = "B" },
        Difficulty.OnLevel, origin);

    [Theory]
    [InlineData(QuestionOrigin.Official, QuestionStatus.InAiReview, QuestionStatus.Live)]
    [InlineData(QuestionOrigin.Official, QuestionStatus.AwaitingOwner, QuestionStatus.Live)]
    [InlineData(QuestionOrigin.Official, QuestionStatus.Draft, QuestionStatus.Live)]
    [InlineData(QuestionOrigin.Official, QuestionStatus.Rejected, QuestionStatus.Rejected)]
    [InlineData(QuestionOrigin.Official, QuestionStatus.Retired, QuestionStatus.Retired)]
    [InlineData(QuestionOrigin.Generated, QuestionStatus.InAiReview, QuestionStatus.InAiReview)]
    [InlineData(QuestionOrigin.Generated, QuestionStatus.AwaitingOwner, QuestionStatus.AwaitingOwner)]
    public async Task Only_released_questions_left_in_review_go_live(
        QuestionOrigin origin, QuestionStatus stored, QuestionStatus expected)
    {
        var question = New(origin);
        await using (var db = NewContext())
        {
            await new EfQuestionRepository(db).AddAsync(question);
            await db.SaveChangesAsync();
            // How an older import left the row; today's domain no longer lets a released question reach this state.
            await db.Questions.Where(q => q.Id == question.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(q => q.Status, stored));
        }

        await using (var db = NewContext())
        {
            var result = await new PublishReleasedQuestionsHandler(new EfQuestionRepository(db), new FixedClock(), db)
                .HandleAsync(new PublishReleasedQuestionsCommand());
            Assert.True(result.IsSuccess);
            Assert.Equal(expected == stored ? 0 : 1, result.Value!.QuestionsPublished);
        }

        await using (var db = NewContext())
        {
            var loaded = await new EfQuestionRepository(db).GetAsync(question.Id);
            Assert.Equal(expected, loaded!.Status);
        }
    }

    [Fact]
    public async Task Running_it_again_publishes_nothing()
    {
        await using var db = NewContext();
        var handler = new PublishReleasedQuestionsHandler(new EfQuestionRepository(db), new FixedClock(), db);
        var result = await handler.HandleAsync(new PublishReleasedQuestionsCommand());
        Assert.Equal(0, result.Value!.QuestionsPublished);
    }

    public void Dispose() => _connection.Dispose();
}
