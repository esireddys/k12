using K12.Domain.Content;
using K12.Domain.Content.Answers;
using K12.Domain.Content.Review;
using K12.Domain.Curriculum;
using K12.Infrastructure.Content;
using K12.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K12.Infrastructure.Tests;

/// <summary>Round-trips the AI review and the owner decision, and checks the queue queries, through the EF mappings.</summary>
public sealed class ContentReviewPersistenceTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public ContentReviewPersistenceTests()
    {
        _connection.Open();
        using var db = NewContext();
        db.Database.EnsureCreated();
    }

    private K12DbContext NewContext() => new(new DbContextOptionsBuilder<K12DbContext>().UseSqlite(_connection).Options);

    [Fact]
    public async Task Reviews_decisions_and_queue_lookups_survive_a_round_trip()
    {
        var course = Course.Create("TX", "math", "G3", "Grade 3", "FW", "Framework", [new ReportingCategory(1, "Numbers")]);
        var skill = Skill.Define(course, "3.4(K)", "Solve", 1, "readiness", "{}");
        var reviewed = Question.Draft([skill.Id], "choice", """{"stem":"a"}""",
            new AnswerSpec { Kind = AnswerKind.Choice, Value = "B" }, Difficulty.OnLevel, QuestionOrigin.Generated);
        var waiting = Question.Draft([skill.Id], "numeric", """{"stem":"b"}""",
            new AnswerSpec { Kind = AnswerKind.Integer, Value = "12" }, Difficulty.OnLevel, QuestionOrigin.Generated);
        reviewed.SubmitForAiReview();
        waiting.SubmitForAiReview();
        reviewed.RecordAiReview(TestReviews.LooksGood("""{"stem":"A."}""") with
        {
            Verdict = AiVerdict.NeedsAttention,
            Findings = [new AiFinding(FindingSeverity.Warning, "figure", "The bar graph has no title in the text.")],
        });
        reviewed.Reject("owner@example.org", DateTimeOffset.UnixEpoch, "Figure is cut off.");
        var form = ExamForm.Create(course.Id, "EXAM", 2021, "spring", "https://example.org/t.pdf");
        form.AddItem(1, reviewed.Id);
        form.AddItem(2, waiting.Id);

        await using (var db = NewContext())
        {
            await new EfCourseRepository(db).AddAsync(course);
            await new EfSkillRepository(db).AddAsync(skill);
            await new EfQuestionRepository(db).AddAsync(reviewed);
            await new EfQuestionRepository(db).AddAsync(waiting);
            await new EfExamFormRepository(db).AddAsync(form);
            await db.SaveChangesAsync();
        }

        await using (var db = NewContext())
        {
            var questions = new EfQuestionRepository(db);
            var loaded = (await questions.GetAsync(reviewed.Id))!;
            Assert.Equal(QuestionStatus.Rejected, loaded.Status);
            Assert.Equal(AiVerdict.NeedsAttention, loaded.AiReview!.Verdict);
            Assert.Equal("figure", Assert.Single(loaded.AiReview.Findings).Area);
            Assert.Equal("""{"stem":"A."}""", loaded.AiReview.ProposedContentJson);
            Assert.Equal(new OwnerDecision(OwnerOutcome.Rejected, "owner@example.org", DateTimeOffset.UnixEpoch, "Figure is cut off."),
                loaded.OwnerDecision);

            var stillWaiting = (await questions.GetAsync(waiting.Id))!;
            Assert.Null(stillWaiting.AiReview);
            Assert.Null(stillWaiting.OwnerDecision);

            Assert.Equal([waiting.Id], (await questions.ListByStatusAsync(QuestionStatus.InAiReview, 10)).Select(q => q.Id));
            var counts = await questions.CountByStatusAsync();
            Assert.Equal((1, 1), (counts[QuestionStatus.InAiReview], counts[QuestionStatus.Rejected]));

            Assert.Equal(form.Id, Assert.Single(await new EfExamFormRepository(db).ListContainingAsync([waiting.Id])).Id);
            Assert.Equal("3.4(K)", Assert.Single(await new EfSkillRepository(db).ListByIdsAsync([skill.Id])).Code);
            Assert.Equal("Grade 3", Assert.Single(await new EfCourseRepository(db).ListByIdsAsync([course.Id])).Name);
        }
    }

    [Fact]
    public async Task A_changed_review_is_saved()
    {
        var q = Question.Draft([SkillId.New()], "numeric", "{}",
            new AnswerSpec { Kind = AnswerKind.Integer, Value = "1" }, Difficulty.OnLevel, QuestionOrigin.Generated);
        q.SubmitForAiReview();
        await using (var db = NewContext())
        {
            await new EfQuestionRepository(db).AddAsync(q);
            await db.SaveChangesAsync();
        }

        await using (var db = NewContext())
        {
            var loaded = (await new EfQuestionRepository(db).GetAsync(q.Id))!;
            loaded.RecordAiReview(TestReviews.LooksGood());
            loaded.Approve("owner", DateTimeOffset.UnixEpoch);
            await db.SaveChangesAsync();
        }

        await using (var db = NewContext())
        {
            var loaded = (await new EfQuestionRepository(db).GetAsync(q.Id))!;
            Assert.Equal(QuestionStatus.Live, loaded.Status);
            Assert.Equal("test-reviewer", loaded.AiReview!.Reviewer);
            Assert.Equal(OwnerOutcome.Approved, loaded.OwnerDecision!.Outcome);
        }
    }

    public void Dispose() => _connection.Dispose();
}

public sealed class FileSystemItemImageStoreTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("k12-images-").FullName;

    public FileSystemItemImageStoreTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "form"));
        File.WriteAllBytes(Path.Combine(_root, "form", "01.png"), [137, 80, 78, 71]);
        File.WriteAllText(Path.Combine(_root, "form.json"), "{}");
        File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(_root)!, "outside.png"), [1]);
    }

    [Theory]
    [InlineData("form/01.png", true)]
    [InlineData("form\\01.png", true)]
    [InlineData("/form/01.png", true)]
    [InlineData("form/02.png", false)]
    [InlineData("form.json", false)]
    [InlineData("../outside.png", false)]
    [InlineData("form/../../outside.png", false)]
    public async Task Serves_only_pictures_inside_the_root(string path, bool found)
    {
        var image = await new FileSystemItemImageStore(_root).OpenAsync(path);
        Assert.Equal(found, image is not null);
        if (image is not null)
        {
            Assert.Equal("image/png", image.ContentType);
            await image.Content.DisposeAsync();
        }
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}

public sealed class QuestionConcurrencyTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public QuestionConcurrencyTests()
    {
        _connection.Open();
        using var db = NewContext();
        db.Database.EnsureCreated();
    }

    private K12DbContext NewContext() => new(new DbContextOptionsBuilder<K12DbContext>().UseSqlite(_connection).Options);

    [Fact]
    public async Task Two_owners_deciding_at_once_cannot_both_win()
    {
        var q = Question.Draft([SkillId.New()], "numeric", "{}",
            new AnswerSpec { Kind = AnswerKind.Integer, Value = "1" }, Difficulty.OnLevel, QuestionOrigin.Generated);
        q.SubmitForAiReview();
        q.RecordAiReview(TestReviews.LooksGood());
        await using (var db = NewContext())
        {
            await new EfQuestionRepository(db).AddAsync(q);
            await db.SaveChangesAsync();
        }

        await using var first = NewContext();
        await using var second = NewContext();
        var a = (await new EfQuestionRepository(first).GetAsync(q.Id))!;
        var b = (await new EfQuestionRepository(second).GetAsync(q.Id))!;

        a.Approve("owner-a", DateTimeOffset.UnixEpoch);
        await ((K12.Application.Abstractions.IUnitOfWork)first).SaveChangesAsync();
        b.Reject("owner-b", DateTimeOffset.UnixEpoch, "no");

        await Assert.ThrowsAsync<K12.Application.Abstractions.ConcurrencyConflictException>(
            () => ((K12.Application.Abstractions.IUnitOfWork)second).SaveChangesAsync());
    }

    public void Dispose() => _connection.Dispose();
}
