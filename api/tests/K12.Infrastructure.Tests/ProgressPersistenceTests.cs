using K12.Domain.Accounts;
using K12.Domain.Content;
using K12.Domain.Content.Answers;
using K12.Domain.Curriculum;
using K12.Domain.Progress;
using K12.Infrastructure.Persistence;
using K12.Infrastructure.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K12.Infrastructure.Tests;

/// <summary>A kid's history, progress and the parent PIN, through the real EF mappings.</summary>
public sealed class ProgressPersistenceTests : IDisposable
{
    private static readonly DateTimeOffset At = new(2026, 11, 27, 15, 0, 0, TimeSpan.Zero);
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public ProgressPersistenceTests()
    {
        _connection.Open();
        using var db = NewContext();
        db.Database.EnsureCreated();
    }

    private K12DbContext NewContext() => new(new DbContextOptionsBuilder<K12DbContext>().UseSqlite(_connection).Options);

    public void Dispose() => _connection.Dispose();

    private async Task<(Course Course, Skill Skill, Question Question, KidProfileId KidId)> SeedAsync()
    {
        var course = Course.Create("TX", "math", "G3", "Grade 3", "FW", "Framework", [new ReportingCategory(1, "Numbers")]);
        var skill = Skill.Define(course, "3.2(A)", "Place value", 1, null, "{}");
        var question = Question.Draft([skill.Id], "numeric", "{}", new AnswerSpec { Kind = AnswerKind.Integer, Value = "1" },
            Difficulty.OnLevel, QuestionOrigin.Official);
        question.PublishReleased(At);
        var account = ParentAccount.Register(new ExternalLogin("google", "1"), "pat@example.org", "Pat", At);
        var kid = account.AddKid("Maya", AvatarColor.Orange, course.Id);

        await using var db = NewContext();
        await new EfCourseRepository(db).AddAsync(course);
        await new EfSkillRepository(db).AddAsync(skill);
        await new EfQuestionRepository(db).AddAsync(question);
        await new EfParentAccountRepository(db).AddAsync(account);
        await db.SaveChangesAsync();
        return (course, skill, question, kid.Id);
    }

    [Fact]
    public async Task Progress_and_history_load_back_whole()
    {
        var (course, skill, question, kidId) = await SeedAsync();
        var sessionId = PracticeSessionId.New();

        await using (var db = NewContext())
        {
            var session = PracticeSession.Start(sessionId, kidId, course.Id, PracticeMode.Exam, "Year", "EXAM 2022", At);
            session.Answer(question.Id, skill.Id, "7", false, At);
            session.AddTime(90, 1);
            session.Finish(At.AddMinutes(2));
            var progress = KidProgress.Start(kidId);
            progress.Record([new AnswerFact(question.Id, skill.Id, true, false)], new DateOnly(2026, 11, 27), 90, At);
            await new EfPracticeSessionRepository(db).AddAsync(session);
            await new EfKidProgressRepository(db).AddAsync(progress);
            await db.SaveChangesAsync();
        }

        await using (var db = NewContext())
        {
            var progress = await new EfKidProgressRepository(db).GetAsync(kidId);
            Assert.NotNull(progress);
            Assert.Equal((skill.Id, 1, 0, "0"), (progress.Skills[0].SkillId, progress.Skills[0].Answered, progress.Skills[0].Correct, progress.Skills[0].Recent));
            Assert.Equal(question.Id, Assert.Single(progress.OpenMistakes).QuestionId);
            Assert.Equal((new DateOnly(2026, 11, 27), 90), (progress.Days[0].Date, progress.Days[0].Seconds));

            // Fixing the mistake and another day save as changes to the same rows.
            progress.Record([new AnswerFact(question.Id, skill.Id, true, true)], new DateOnly(2026, 11, 28), 30, At.AddDays(1));
            await db.SaveChangesAsync();

            var recent = await new EfPracticeSessionRepository(db).ListRecentAsync(kidId, 10);
            var session = Assert.Single(recent);
            Assert.Equal((sessionId, PracticeMode.Exam, "EXAM 2022", 0, 1, 2), (session.Id, session.Mode, session.Title, session.Correct, session.Total, session.Minutes));
            Assert.Equal("7", session.Answers[0].Response);
            Assert.NotNull(session.FinishedAt);

            var inCourse = Assert.Single(await new EfPracticeSessionRepository(db).ListByCourseAsync(kidId, course.Id));
            Assert.Equal(question.Id, Assert.Single(inCourse.Answers).QuestionId);
            Assert.Empty(await new EfPracticeSessionRepository(db).ListByCourseAsync(kidId, CourseId.New()));
        }

        await using (var db = NewContext())
        {
            var progress = (await new EfKidProgressRepository(db).GetAsync(kidId))!;
            Assert.Empty(progress.OpenMistakes);
            Assert.Equal(2, progress.Days.Count);
            Assert.Equal(2, progress.CurrentStreak(new DateOnly(2026, 11, 29))); // two days in a row, still standing on the 29th

            new EfKidProgressRepository(db).Remove(progress);
            await new EfPracticeSessionRepository(db).RemoveAllForKidAsync(kidId);
            await db.SaveChangesAsync();
        }

        await using (var db = NewContext())
        {
            Assert.Null(await new EfKidProgressRepository(db).GetAsync(kidId));
            Assert.Empty(await new EfPracticeSessionRepository(db).ListRecentAsync(kidId, 10));
        }
    }

    [Fact]
    public async Task The_parent_pin_saves_only_a_hash_with_its_lock()
    {
        var hasher = new Pbkdf2PinHasher();
        var account = ParentAccount.Register(new ExternalLogin("google", "2"), "sam@example.org", "Sam", At);
        account.SetPin(hasher.Hash("4321"), At);
        for (var i = 0; i < ParentPin.MaxWrongTries; i++) account.CheckPin(false, At);

        await using (var db = NewContext())
        {
            await new EfParentAccountRepository(db).AddAsync(account);
            await db.SaveChangesAsync();
        }

        await using (var db = NewContext())
        {
            var loaded = (await new EfParentAccountRepository(db).GetAsync(account.Id))!;
            Assert.DoesNotContain("4321", loaded.Pin!.Hash);
            Assert.True(hasher.Verify("4321", loaded.Pin.Hash));
            Assert.False(hasher.Verify("1234", loaded.Pin.Hash));
            Assert.True(loaded.Pin.IsLocked(At.AddMinutes(1)));
        }

        // An account with no PIN loads with none.
        var noPin = ParentAccount.Register(new ExternalLogin("google", "3"), "lee@example.org", "Lee", At);
        await using (var db = NewContext())
        {
            await new EfParentAccountRepository(db).AddAsync(noPin);
            await db.SaveChangesAsync();
        }
        await using (var db = NewContext())
            Assert.False((await new EfParentAccountRepository(db).GetAsync(noPin.Id))!.HasPin);
    }

    [Theory]
    [InlineData("v1.x.abc.def")]
    [InlineData("nonsense")]
    [InlineData("v1.100000.!!!.???")]
    public void A_damaged_hash_never_matches(string stored) => Assert.False(new Pbkdf2PinHasher().Verify("1234", stored));
}
