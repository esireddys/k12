using K12.Domain.Accounts;
using K12.Domain.Common;
using K12.Domain.Content;
using K12.Domain.Curriculum;
using K12.Domain.Progress;

namespace K12.Domain.Tests.Progress;

public class PracticeSessionTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 27, 15, 0, 0, TimeSpan.Zero);

    private static PracticeSession Session(PracticeMode mode = PracticeMode.Practice, string title = "Place value") =>
        PracticeSession.Start(PracticeSessionId.New(), KidProfileId.New(), CourseId.New(), mode, "Topic", title, At);

    [Fact]
    public void Only_the_first_answer_to_a_question_counts()
    {
        var s = Session();
        var q = QuestionId.New();
        Assert.True(s.Answer(q, null, "12", false, At));
        Assert.False(s.Answer(q, null, "13", true, At.AddMinutes(1)));
        var answer = Assert.Single(s.Answers);
        Assert.Equal(("12", false), (answer.Response, answer.IsCorrect));
        Assert.Equal((0, 1), (s.Correct, s.Total));
    }

    [Fact]
    public void A_finished_set_takes_no_more_answers_and_finishing_twice_is_harmless()
    {
        var s = Session(PracticeMode.Exam);
        s.Answer(QuestionId.New(), null, "B", true, At);
        s.Finish(At.AddMinutes(20));
        s.Finish(At.AddMinutes(30));
        Assert.Equal(At.AddMinutes(20), s.FinishedAt);
        Assert.IsType<PracticeSessionFinished>(Assert.Single(s.DomainEvents));
        Assert.Throws<DomainException>(() => s.Answer(QuestionId.New(), null, "A", true, At));
    }

    [Theory]
    [InlineData("", "Practice")]
    [InlineData("  2022  ", "2022")]
    public void The_title_is_trimmed_and_never_empty(string title, string expected) =>
        Assert.Equal(expected, Session(title: title).Title);

    [Fact]
    public void Long_titles_and_responses_are_cut_to_fit()
    {
        var s = Session(title: new string('x', 200));
        s.Answer(QuestionId.New(), null, new string('9', 100), false, At);
        Assert.Equal(PracticeSession.MaxTitleLength, s.Title.Length);
        Assert.Equal(SessionAnswer.MaxResponseLength, s.Answers[0].Response.Length);
    }

    [Theory]
    [InlineData(0, 0, 0)] // nothing answered
    [InlineData(20, 1, 1)] // at least a minute once anything was answered
    [InlineData(170, 1, 3)]
    [InlineData(99999, 2, 20)] // capped at 10 minutes an answer
    public void Minutes_come_from_capped_device_time(int seconds, int answers, int expected)
    {
        var s = Session();
        for (var i = 0; i < answers; i++) s.Answer(QuestionId.New(), null, "1", true, At);
        s.AddTime(seconds, answers);
        Assert.Equal(expected, s.Minutes);
    }

    [Fact]
    public void A_set_has_at_most_a_hundred_answers()
    {
        var s = Session();
        for (var i = 0; i < PracticeSession.MaxAnswers; i++) s.Answer(QuestionId.New(), null, "1", true, At);
        Assert.Throws<DomainException>(() => s.Answer(QuestionId.New(), null, "1", true, At));
    }
}
