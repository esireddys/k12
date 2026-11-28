using K12.Domain.Common;
using K12.Domain.Content;
using K12.Domain.Content.Answers;
using K12.Domain.Content.Reports;
using K12.Domain.Curriculum;

namespace K12.Domain.Tests.Content;

public class QuestionReportTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);

    private static Question Released(bool live = true)
    {
        var q = Question.Draft([SkillId.New()], "choice", """{"stem":"Which?"}""",
            new AnswerSpec { Kind = AnswerKind.Choice, Value = "B" }, Difficulty.OnLevel, QuestionOrigin.Official);
        if (live) q.PublishReleased(At);
        return q;
    }

    [Theory]
    [InlineData(ReportReason.WrongAnswer, Reporter.Kid)]
    [InlineData(ReportReason.HardToRead, Reporter.Guest)]
    [InlineData(ReportReason.Typo, Reporter.Parent)]
    public void A_live_question_can_be_reported_with_a_reason_and_who_sent_it(ReportReason reason, Reporter reporter)
    {
        var q = Released();
        var report = QuestionReport.File(q, reason, reporter, At);
        Assert.Equal((q.Id, q.Version, reason, reporter, true), (report.QuestionId, report.QuestionVersion, report.Reason, report.Reporter, report.IsOpen));
        Assert.IsType<QuestionReported>(Assert.Single(report.DomainEvents));
    }

    [Fact]
    public void Only_a_question_students_can_see_is_reported()
    {
        Assert.Throws<DomainException>(() => QuestionReport.File(Released(live: false), ReportReason.Other, Reporter.Kid, At));
    }

    [Theory]
    [InlineData((ReportReason)99, Reporter.Kid)]
    [InlineData(ReportReason.Other, (Reporter)99)]
    public void Unknown_reasons_and_reporters_are_refused(ReportReason reason, Reporter reporter)
    {
        Assert.Throws<DomainException>(() => QuestionReport.File(Released(), reason, reporter, At));
    }

    [Fact]
    public void An_owner_closes_a_report_once()
    {
        var report = QuestionReport.File(Released(), ReportReason.Confusing, Reporter.Kid, At);
        Assert.Throws<DomainException>(() => report.Close(" ", At));
        report.Close(" owner@example.org ", At.AddHours(1));
        Assert.Equal(("owner@example.org", false), (report.ClosedBy, report.IsOpen));
        Assert.Throws<DomainException>(() => report.Close("owner@example.org", At.AddHours(2)));
    }
}
