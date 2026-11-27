using System.Text.Json;
using K12.Application.ContentReview;
using K12.Application.Practice.Sets;
using K12.Domain.Content;
using K12.Domain.Content.Answers;
using K12.Domain.Content.Reports;
using K12.Domain.Curriculum;

namespace K12.Application.Tests.ContentReview;

public class ReportsAndBrowserTests
{
    /// <summary>Grade 3 math with a 2022 form of three Live released items; item 2 has an explanation.</summary>
    private sealed class Bank
    {
        public TestStore S { get; } = new();
        public Course Course { get; }
        public List<Question> Items { get; } = [];

        public Bank()
        {
            Course = Course.Create("TX", "math", "G3", "Grade 3", "FW", "Framework", [new ReportingCategory(1, "Numbers")]);
            var skill = Skill.Define(Course, "3.4(K)", "Solve", 1, null, "{}");
            var form = ExamForm.Create(Course.Id, "EXAM", 2022, "spring", "https://example.org/t.pdf");
            S.Courses.Add(Course);
            S.Skills.Add(skill);
            for (var n = 1; n <= 3; n++)
            {
                var content = n == 2 ? """{"stem":"Two","explanation":"Because."}""" : $$"""{"stem":"Item {{n}}","unit":"square feet"}""";
                var q = Question.Draft([skill.Id], "numeric", content,
                    new AnswerSpec { Kind = AnswerKind.Integer, Value = $"{n}" }, Difficulty.OnLevel, QuestionOrigin.Official);
                q.PublishReleased(S.UtcNow);
                form.AddItem(n, q.Id);
                S.Questions.Add(q);
                Items.Add(q);
            }
            S.Forms.Add(form);
        }

        public Task<Abstractions.Result<QuestionReportFiled>> Report(Question q, ReportReason reason = ReportReason.WrongAnswer) =>
            new ReportQuestionHandler(S, S, S, S).HandleAsync(new ReportQuestionCommand(q.Id.Value, reason, Reporter.Kid));

        public async Task<BrowseQuestionsResponse> Browse(BrowseQuestionsQuery query) =>
            (await new BrowseQuestionsHandler(S, S, S, S, S).HandleAsync(query)).Value!;
    }

    [Fact]
    public async Task A_report_lands_in_the_owners_list_with_where_the_question_came_from()
    {
        var b = new Bank();
        Assert.True((await b.Report(b.Items[0])).IsSuccess);

        var list = (await new ListQuestionReportsHandler(b.S, b.S, b.S, b.S, b.S).HandleAsync(new ListQuestionReportsQuery())).Value!;
        var report = Assert.Single(list.Reports);
        Assert.Equal((b.Items[0].Id.Value, ReportReason.WrongAnswer, Reporter.Kid, false), (report.QuestionId, report.Reason, report.Reporter, report.Fixed));
        Assert.Equal((2022, 1), (report.Source!.Year, report.Source.ItemNumber));
    }

    [Fact]
    public async Task A_question_kids_cannot_see_cannot_be_reported()
    {
        var b = new Bank();
        b.Items[0].Retire();
        var result = await b.Report(b.Items[0]);
        Assert.Equal("question.not_found", result.Error!.Code);
        Assert.Empty(b.S.Reports);
    }

    public static TheoryData<string, BrowseQuestionsQuery, int[]> Filters => new()
    {
        { "everything Live", new BrowseQuestionsQuery(), [1, 2, 3] },
        { "missing an explanation", new BrowseQuestionsQuery(HasExplanation: false), [1, 3] },
        { "with one", new BrowseQuestionsQuery(HasExplanation: true), [2] },
        { "by year", new BrowseQuestionsQuery(Year: 2021), [] },
        { "by skill", new BrowseQuestionsQuery(SkillCode: "3.4(k)"), [1, 2, 3] },
        { "another skill", new BrowseQuestionsQuery(SkillCode: "9.9(Z)"), [] },
        { "reported first, then only reported", new BrowseQuestionsQuery(Reported: true), [3] },
    };

    [Theory]
    [MemberData(nameof(Filters))]
    public async Task Owners_can_find_live_questions_by_filter(string _, BrowseQuestionsQuery query, int[] items)
    {
        var b = new Bank();
        await b.Report(b.Items[2]);
        var found = await b.Browse(query);
        Assert.Equal(items.Order(), found.Items.Select(i => i.Source!.ItemNumber).Order());
        Assert.Equal(items.Length, found.Total);
    }

    [Fact]
    public async Task Reported_questions_come_first()
    {
        var b = new Bank();
        await b.Report(b.Items[2]);
        await b.Report(b.Items[2], ReportReason.Typo);
        var first = (await b.Browse(new BrowseQuestionsQuery())).Items[0];
        Assert.Equal((3, 2), (first.Source!.ItemNumber, first.OpenReports));
    }

    [Fact]
    public async Task An_owner_fix_keeps_a_released_question_live_and_closes_its_reports()
    {
        var b = new Bank();
        var q = b.Items[0];
        await b.Report(q);
        using var content = JsonDocument.Parse("""{"stem":"Item 1","explanation":"Count the squares.","textNeedsReview":true}""");

        var result = await new ReviseLiveQuestionHandler(b.S, b.S, b.S, b.S)
            .HandleAsync(new ReviseLiveQuestionCommand(q.Id.Value, 1, "owner@example.org", content.RootElement, null));

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal((QuestionStatus.Live, 2), (q.Status, q.Version));
        Assert.Contains("Count the squares.", q.ContentJson);
        Assert.DoesNotContain("textNeedsReview", q.ContentJson);
        Assert.False(Assert.Single(b.S.Reports).IsOpen);
    }

    [Fact]
    public async Task A_stale_or_non_live_edit_is_refused()
    {
        var b = new Bank();
        using var content = JsonDocument.Parse("""{"stem":"x"}""");
        var handler = new ReviseLiveQuestionHandler(b.S, b.S, b.S, b.S);
        Assert.Equal("review.stale", (await handler.HandleAsync(new ReviseLiveQuestionCommand(b.Items[0].Id.Value, 5, "o", content.RootElement, null))).Error!.Code);
        b.Items[1].Retire();
        Assert.Equal("review.wrong_state", (await handler.HandleAsync(new ReviseLiveQuestionCommand(b.Items[1].Id.Value, 1, "o", content.RootElement, null))).Error!.Code);
    }

    [Fact]
    public async Task Closing_reports_marks_every_open_one_on_the_question()
    {
        var b = new Bank();
        await b.Report(b.Items[0]);
        await b.Report(b.Items[0], ReportReason.HardToRead);
        await b.Report(b.Items[1]);
        var closed = await new CloseQuestionReportsHandler(b.S, b.S, b.S).HandleAsync(new CloseQuestionReportsCommand(b.Items[0].Id.Value, "owner"));
        Assert.Equal(2, closed.Value!.Closed);
        Assert.Single(b.S.Reports, r => r.IsOpen);
    }

    [Fact]
    public async Task A_typed_answer_shows_its_unit_and_a_choice_never_does()
    {
        var b = new Bank();
        var set = (await new GetPracticeSetHandler(b.S, b.S, b.S, b.S, b.S)
            .HandleAsync(new GetPracticeSetQuery(b.Course.Id.Value, SetSource.Year, b.S.Forms[0].Id.Value))).Value!;
        Assert.Equal(["square feet", null, "square feet"], set.Questions.Select(q => q.Unit));
    }
}
