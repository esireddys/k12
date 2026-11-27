using K12.Application.Accounts;
using K12.Application.Practice.Sets;
using K12.Application.Progress;
using K12.Domain.Accounts;
using K12.Domain.Content;
using K12.Domain.Content.Answers;
using K12.Domain.Curriculum;
using K12.Domain.Progress;

namespace K12.Application.Tests.Progress;

public class ProgressHandlerTests
{
    /// <summary>A course with two skills and four Live questions, and a family with one kid in that course.</summary>
    private sealed class Family
    {
        public TestStore S { get; } = new();
        public Course Course { get; }
        public Skill Numbers { get; }
        public Skill Shapes { get; }
        public List<Question> Questions { get; } = [];
        public ParentAccount Account { get; }
        public KidProfile Kid { get; }
        public DateOnly Today => DateOnly.FromDateTime(S.UtcNow.UtcDateTime);

        public Family()
        {
            Course = Course.Create("TX", "math", "G3", "Grade 3", "FW", "Framework", [new ReportingCategory(1, "Numbers"), new ReportingCategory(2, "Shapes")]);
            Numbers = Skill.Define(Course, "3.2(A)", "Place value", 1, "readiness", "{}");
            Shapes = Skill.Define(Course, "3.6(A)", "Shapes", 2, "supporting", "{}");
            S.Courses.Add(Course);
            S.Skills.AddRange([Numbers, Shapes]);
            for (var n = 1; n <= 4; n++)
            {
                var q = Question.Draft([n <= 3 ? Numbers.Id : Shapes.Id], "numeric", $$"""{"stem":"Item {{n}}"}""",
                    new AnswerSpec { Kind = AnswerKind.Integer, Value = $"{n}" }, Difficulty.OnLevel, QuestionOrigin.Official);
                q.PublishReleased(S.UtcNow);
                S.Questions.Add(q);
                Questions.Add(q);
            }

            Account = ParentAccount.Register(new ExternalLogin("google", "1"), "pat@example.org", "Pat", S.UtcNow);
            Kid = Account.AddKid("Maya", AvatarColor.Orange, Course.Id);
            S.Accounts.Add(Account);
        }

        public RecordAnswersHandler Record => new(S, S, S, S, S, S, S);

        public Task<Application.Abstractions.Result<RecordedScore>> AnswerAsync(
            Guid session, PracticeMode mode, bool finish, params (int Item, string Response)[] answers) =>
            Record.HandleAsync(new RecordAnswersCommand(
                Account.Id.Value, Kid.Id.Value, session, mode, "Topic", "Place value", Today, 60,
                answers.Select(a => new ResponseItem(Questions[a.Item - 1].Id.Value, a.Response)).ToList(), finish));
    }

    [Fact]
    public async Task A_kids_answers_are_scored_and_saved_to_history_and_progress()
    {
        var f = new Family();
        var session = Guid.NewGuid();

        var first = await f.AnswerAsync(session, PracticeMode.Practice, false, (1, "1"));
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.True(first.Value!.Score.Items[0].IsCorrect);
        var second = await f.AnswerAsync(session, PracticeMode.Practice, false, (2, "99"));
        Assert.Equal((1, 2), (second.Value!.Session.Correct, second.Value.Session.Total));

        var saved = Assert.Single(f.S.Sessions);
        Assert.Equal((f.Kid.Id, f.Course.Id, "Place value"), (saved.KidId, saved.CourseId, saved.Title));
        var progress = Assert.Single(f.S.Progress);
        Assert.Equal((2, 1), (progress.TotalAnswered, progress.TotalCorrect));
        Assert.Equal(f.Questions[1].Id, Assert.Single(progress.OpenMistakes).QuestionId);
        Assert.Equal(f.Numbers.Id, Assert.Single(progress.Skills).SkillId);
    }

    [Fact]
    public async Task Answering_the_same_question_again_in_a_set_changes_nothing()
    {
        var f = new Family();
        var session = Guid.NewGuid();
        await f.AnswerAsync(session, PracticeMode.Practice, false, (2, "99"));
        var again = await f.AnswerAsync(session, PracticeMode.Practice, false, (2, "2"));

        Assert.True(again.Value!.Score.Items[0].IsCorrect); // the kid still sees the check
        Assert.Equal((0, 1), (again.Value.Session.Correct, again.Value.Session.Total));
        Assert.Single(f.S.Progress[0].OpenMistakes);
        Assert.Equal(1, f.S.Progress[0].TotalAnswered);
    }

    [Fact]
    public async Task An_exam_sends_every_answer_and_finishes_in_one_call()
    {
        var f = new Family();
        var result = await f.AnswerAsync(Guid.NewGuid(), PracticeMode.Exam, true, (1, "1"), (2, "2"), (4, ""));
        Assert.NotNull(result.Value!.Session.FinishedAt);
        Assert.Equal((2, 3), (result.Value.Session.Correct, result.Value.Session.Total));
        Assert.Single(f.S.Progress[0].OpenMistakes); // the blank one
    }

    [Fact]
    public async Task Fix_my_mistakes_brings_back_missed_questions_until_they_are_right()
    {
        var f = new Family();
        await f.AnswerAsync(Guid.NewGuid(), PracticeMode.Exam, true, (1, "0"), (2, "0"), (3, "3"));

        var mistakes = new GetMistakesSetHandler(f.S, f.S, f.S, f.S, f.S, f.S);
        var set = await mistakes.HandleAsync(new GetMistakesSetQuery(f.Account.Id.Value, f.Kid.Id.Value, null));
        Assert.Equal(SetSource.Mistakes, set.Value!.Source);
        Assert.Equal(new[] { f.Questions[0].Id.Value, f.Questions[1].Id.Value }.Order(), set.Value.Questions.Select(q => q.Id).Order());

        var summary = new GetKidSummaryHandler(f.S, f.S, f.S, f.S, f.S);
        Assert.Equal(2, (await summary.HandleAsync(new GetKidSummaryQuery(f.Account.Id.Value, f.Kid.Id.Value, f.Today))).Value!.MistakesToFix);

        // Retrying: one right, one still wrong.
        await f.AnswerAsync(Guid.NewGuid(), PracticeMode.Practice, false, (1, "1"));
        await f.AnswerAsync(Guid.NewGuid(), PracticeMode.Practice, false, (2, "5"));
        var after = await mistakes.HandleAsync(new GetMistakesSetQuery(f.Account.Id.Value, f.Kid.Id.Value, null));
        Assert.Equal(f.Questions[1].Id.Value, Assert.Single(after.Value!.Questions).Id);

        var s = (await summary.HandleAsync(new GetKidSummaryQuery(f.Account.Id.Value, f.Kid.Id.Value, f.Today))).Value!;
        Assert.Equal((1, 1, 2, 5), (s.MistakesToFix, s.Streak, s.Stars, s.Answered));
    }

    [Fact]
    public async Task A_question_taken_down_after_a_miss_never_comes_back()
    {
        var f = new Family();
        await f.AnswerAsync(Guid.NewGuid(), PracticeMode.Practice, false, (1, "0"));
        f.Questions[0].Retire();
        var set = await new GetMistakesSetHandler(f.S, f.S, f.S, f.S, f.S, f.S).HandleAsync(new GetMistakesSetQuery(f.Account.Id.Value, f.Kid.Id.Value, null));
        Assert.Empty(set.Value!.Questions);
    }

    [Fact]
    public async Task The_dashboard_sums_up_the_week_skills_tests_and_history()
    {
        var f = new Family();
        await f.AnswerAsync(Guid.NewGuid(), PracticeMode.Exam, true, (1, "0"), (2, "0"), (3, "0"), (4, "4"));
        f.S.UtcNow = f.S.UtcNow.AddHours(1);
        await f.AnswerAsync(Guid.NewGuid(), PracticeMode.Exam, true, (1, "1"), (2, "2"), (3, "0"), (4, "4"));

        var d = (await new GetKidDashboardHandler(f.S, f.S, f.S, f.S, f.S, f.S, f.S)
            .HandleAsync(new GetKidDashboardQuery(f.Account.Id.Value, f.Kid.Id.Value, f.Today))).Value!;

        Assert.Equal(("Maya", "Grade 3", "Framework"), (d.Name, d.CourseName, d.FrameworkName));
        Assert.Equal((1, 2, 8, 4), (d.Streak, d.Week.Minutes, d.Answered, d.Correct));
        Assert.Equal((75, 25), (d.LastExam!.Percent, d.LastExam.PreviousPercent!.Value));
        Assert.Equal(2, d.RecentSessions.Count);
        Assert.Equal((0, 2), (d.SkillsMastered, d.SkillsTotal));
        var attention = Assert.Single(d.NeedsAttention);
        Assert.Equal(("3.2(A)", 33), (attention.Code, attention.Accuracy!.Value));
    }

    [Fact]
    public async Task A_parent_only_reaches_their_own_kids()
    {
        var f = new Family();
        var other = ParentAccount.Register(new ExternalLogin("google", "2"), "sam@example.org", "Sam", f.S.UtcNow);
        f.S.Accounts.Add(other);

        var result = await f.Record.HandleAsync(new RecordAnswersCommand(
            other.Id.Value, f.Kid.Id.Value, Guid.NewGuid(), PracticeMode.Practice, "Topic", "x", f.Today, 1,
            [new ResponseItem(f.Questions[0].Id.Value, "1")], false));
        Assert.Equal("family.no_kid", result.Error?.Code);
        Assert.Empty(f.S.Sessions);
    }

    [Fact]
    public async Task A_set_id_used_by_another_kid_is_refused()
    {
        var f = new Family();
        var session = Guid.NewGuid();
        await f.AnswerAsync(session, PracticeMode.Practice, false, (1, "1"));
        var sibling = f.Account.AddKid("Leo", AvatarColor.Blue, f.Course.Id);

        var result = await f.Record.HandleAsync(new RecordAnswersCommand(
            f.Account.Id.Value, sibling.Id.Value, session, PracticeMode.Practice, "Topic", "x", f.Today, 1,
            [new ResponseItem(f.Questions[1].Id.Value, "2")], false));
        Assert.Equal("progress.session_taken", result.Error?.Code);
    }

    [Fact]
    public async Task A_kid_with_no_grade_cannot_record()
    {
        var f = new Family();
        var noGrade = f.Account.AddKid("Leo", AvatarColor.Blue, null);
        var result = await f.Record.HandleAsync(new RecordAnswersCommand(
            f.Account.Id.Value, noGrade.Id.Value, Guid.NewGuid(), PracticeMode.Practice, "Topic", "x", f.Today, 1,
            [new ResponseItem(f.Questions[0].Id.Value, "1")], false));
        Assert.Equal("progress.no_course", result.Error?.Code);
    }

    [Fact]
    public async Task Finishing_a_practice_set_marks_it_done()
    {
        var f = new Family();
        var session = Guid.NewGuid();
        await f.AnswerAsync(session, PracticeMode.Practice, false, (1, "1"));
        var finished = await new FinishSessionHandler(f.S, f.S, f.S, f.S).HandleAsync(new FinishSessionCommand(f.Account.Id.Value, f.Kid.Id.Value, session));
        Assert.NotNull(finished.Value!.FinishedAt);

        var missing = await new FinishSessionHandler(f.S, f.S, f.S, f.S).HandleAsync(new FinishSessionCommand(f.Account.Id.Value, f.Kid.Id.Value, Guid.NewGuid()));
        Assert.Equal("progress.no_session", missing.Error?.Code);
    }

    [Fact]
    public async Task Removing_a_kid_forgets_their_history_and_progress()
    {
        var f = new Family();
        await f.AnswerAsync(Guid.NewGuid(), PracticeMode.Practice, false, (1, "0"));
        await new RemoveKidHandler(f.S, f.S, f.S, f.S).HandleAsync(new RemoveKidCommand(f.Account.Id.Value, f.Kid.Id.Value));
        Assert.Empty(f.S.Sessions);
        Assert.Empty(f.S.Progress);
    }
}
