using K12.Domain.Content;
using K12.Domain.Content.Answers;
using K12.Domain.Content.Reports;
using K12.Domain.Curriculum;
using K12.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K12.Infrastructure.Tests;

/// <summary>Round-trips problem reports and a category's kid name through the EF mappings.</summary>
public sealed class QuestionReportPersistenceTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public QuestionReportPersistenceTests()
    {
        _connection.Open();
        using var db = NewContext();
        db.Database.EnsureCreated();
    }

    private K12DbContext NewContext() => new(new DbContextOptionsBuilder<K12DbContext>().UseSqlite(_connection).Options);

    [Fact]
    public async Task Reports_and_kid_names_survive_a_round_trip()
    {
        var at = new DateTimeOffset(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);
        var course = Course.Create("TX", "math", "G3", "Grade 3", "FW", "Framework", [new ReportingCategory(1, "Geometry", "Shapes")]);
        var skill = Skill.Define(course, "3.6(A)", "Shapes", 1, null, "{}");
        var question = Question.Draft([skill.Id], "choice", """{"stem":"a"}""",
            new AnswerSpec { Kind = AnswerKind.Choice, Value = "B" }, Difficulty.OnLevel, QuestionOrigin.Official);
        question.PublishReleased(at);
        var open = QuestionReport.File(question, ReportReason.HardToRead, Reporter.Kid, at);
        var closed = QuestionReport.File(question, ReportReason.Typo, Reporter.Guest, at.AddMinutes(1));
        closed.Close("owner@example.org", at.AddHours(1));

        await using (var db = NewContext())
        {
            db.AddRange(course, skill, question, open, closed);
            await db.SaveChangesAsync();
        }

        await using (var db = NewContext())
        {
            var repo = new EfQuestionReportRepository(db);
            var stillOpen = Assert.Single(await repo.ListOpenAsync(10));
            Assert.Equal((open.Id, ReportReason.HardToRead, Reporter.Kid, 1), (stillOpen.Id, stillOpen.Reason, stillOpen.Reporter, stillOpen.QuestionVersion));
            Assert.Equal(1, (await repo.CountOpenByQuestionAsync())[question.Id]);
            Assert.Single(await repo.ListOpenForAsync(question.Id));
            Assert.Equal("owner@example.org", (await repo.GetAsync(closed.Id))!.ClosedBy);

            var loaded = await db.Courses.SingleAsync();
            Assert.Equal(("Geometry", "Shapes"), (loaded.Categories[0].Name, loaded.Categories[0].NameForKids));
        }
    }

    public void Dispose() => _connection.Dispose();
}
