using K12.Domain.Accounts;
using K12.Domain.Content;
using K12.Domain.Content.Answers;
using K12.Domain.Curriculum;
using K12.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K12.Infrastructure.Tests;

/// <summary>Parent accounts with their kids, and the lookups the practice screens use, through the real EF mappings.</summary>
public sealed class ParentAccountPersistenceTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public ParentAccountPersistenceTests()
    {
        _connection.Open();
        using var db = NewContext();
        db.Database.EnsureCreated();
    }

    private K12DbContext NewContext() => new(new DbContextOptionsBuilder<K12DbContext>().UseSqlite(_connection).Options);

    private async Task<Course> SaveCourseAsync()
    {
        var course = Course.Create("TX", "math", "G3", "Grade 3", "FW", "Framework", [new ReportingCategory(1, "Numbers")]);
        course.NameJurisdiction("Texas");
        await using var db = NewContext();
        await new EfCourseRepository(db).AddAsync(course);
        await db.SaveChangesAsync();
        return course;
    }

    [Fact]
    public async Task An_account_loads_back_by_login_with_its_kids()
    {
        var course = await SaveCourseAsync();
        var account = ParentAccount.Register(new ExternalLogin("google", "sub-1"), "pat@example.org", "Pat", DateTimeOffset.UnixEpoch);
        account.AddKid("Maya", AvatarColor.Orange, course.Id);
        account.AddKid("Leo", AvatarColor.Blue, null);

        await using (var db = NewContext())
        {
            await new EfParentAccountRepository(db).AddAsync(account);
            await db.SaveChangesAsync();
        }

        await using (var db = NewContext())
        {
            var loaded = await new EfParentAccountRepository(db).FindByLoginAsync(new ExternalLogin("Google", "sub-1"));
            Assert.NotNull(loaded);
            Assert.Equal(account.Id, loaded.Id);
            Assert.Equal("pat@example.org", loaded.Email);
            Assert.Equal(
                [("Maya", AvatarColor.Orange, (CourseId?)course.Id), ("Leo", AvatarColor.Blue, null)],
                loaded.Kids.Select(k => (k.Name, k.Avatar, k.CourseId)).OrderByDescending(k => k.Name == "Maya"));

            loaded.RemoveKid(loaded.Kids.Single(k => k.Name == "Leo").Id);
            loaded.UpdateKid(loaded.Kids.Single().Id, "Maya R", AvatarColor.Teal, null);
            await db.SaveChangesAsync();
        }

        await using (var db = NewContext())
        {
            var loaded = await new EfParentAccountRepository(db).GetAsync(account.Id);
            var kid = Assert.Single(loaded!.Kids);
            Assert.Equal(("Maya R", AvatarColor.Teal, (CourseId?)null), (kid.Name, kid.Avatar, kid.CourseId));
            Assert.Null(await new EfParentAccountRepository(db).FindByLoginAsync(new ExternalLogin("google", "someone-else")));
        }
    }

    [Fact]
    public async Task One_login_has_one_account()
    {
        await using var db = NewContext();
        var repo = new EfParentAccountRepository(db);
        await repo.AddAsync(ParentAccount.Register(new ExternalLogin("google", "same"), "a@example.org", "A", DateTimeOffset.UnixEpoch));
        await repo.AddAsync(ParentAccount.Register(new ExternalLogin("google", "same"), "b@example.org", "B", DateTimeOffset.UnixEpoch));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Courses_forms_and_questions_list_for_the_practice_screens()
    {
        var course = await SaveCourseAsync();
        var skill = Skill.Define(course, "3.2(A)", "Place value", 1, null, "{}");
        var q1 = Question.Draft([skill.Id], "numeric", "{}", new AnswerSpec { Kind = AnswerKind.Integer, Value = "1" }, Difficulty.OnLevel, QuestionOrigin.Official);
        var q2 = Question.Draft([skill.Id], "numeric", "{}", new AnswerSpec { Kind = AnswerKind.Integer, Value = "2" }, Difficulty.OnLevel, QuestionOrigin.Official);
        var form = ExamForm.Create(course.Id, "EXAM", 2022, "spring", "https://example.org");
        form.AddItem(1, q1.Id);
        form.AddItem(2, q2.Id);

        await using (var db = NewContext())
        {
            db.AddRange(skill, q1, q2, form);
            await db.SaveChangesAsync();
        }

        await using (var db = NewContext())
        {
            Assert.Equal("Texas", Assert.Single(await new EfCourseRepository(db).ListAllAsync()).JurisdictionName);
            Assert.Equal(form.Id, Assert.Single(await new EfExamFormRepository(db).ListByCourseAsync(course.Id)).Id);
            Assert.Equal(2, (await new EfExamFormRepository(db).GetAsync(form.Id))!.Items.Count);
            Assert.Equal([q2.Id], (await new EfQuestionRepository(db).ListByIdsAsync([q2.Id])).Select(q => q.Id));
        }
    }

    public void Dispose() => _connection.Dispose();
}
