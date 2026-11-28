using K12.Domain.Common;
using K12.Domain.Content;
using K12.Domain.Content.Answers;
using K12.Domain.Curriculum;

namespace K12.Domain.Tests.Content;

public class PassageTests
{
    private static readonly CourseId Course = CourseId.New();

    [Theory]
    [InlineData("The Unwelcome Neighbor", """{"pages":["a.png"]}""", null)]
    [InlineData("  Rain  ", "{}", null)]
    [InlineData("", "{}", "needs a title")]
    [InlineData("   ", "{}", "needs a title")]
    [InlineData("Rain", "[]", "JSON object")]
    [InlineData("Rain", "not json", "not valid JSON")]
    public void A_passage_needs_a_title_and_object_content(string title, string content, string? error)
    {
        if (error is null)
        {
            var passage = Passage.Create(Course, title, content, QuestionOrigin.Official);
            Assert.Equal(title.Trim(), passage.Title);
            Assert.Equal(Course, passage.CourseId);
        }
        else
        {
            var e = Assert.Throws<DomainException>(() => Passage.Create(Course, title, content, QuestionOrigin.Official));
            Assert.Contains(error, e.Message);
        }
    }

    [Fact]
    public void A_title_longer_than_the_column_is_refused()
    {
        Assert.Throws<DomainException>(() =>
            Passage.Create(Course, new string('x', Passage.MaxTitleLength + 1), "{}", QuestionOrigin.Official));
    }

    [Fact]
    public void Revising_changes_title_and_content()
    {
        var passage = Passage.Create(Course, "Rain", "{}", QuestionOrigin.Official);
        passage.Revise("Rain Again", """{"text":"It rains."}""");
        Assert.Equal("Rain Again", passage.Title);
        Assert.Contains("It rains.", passage.ContentJson);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(1, true)]
    [InlineData(3, true)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    public void A_question_can_name_its_passage_and_its_own_reporting_category(int? category, bool ok)
    {
        var passage = PassageId.New();
        Question Make() => Question.Draft([SkillId.New()], "choice", "{}", new AnswerSpec { Kind = AnswerKind.Choice, Value = "A" },
            Difficulty.OnLevel, QuestionOrigin.Official, passageId: passage, reportingCategory: category);
        if (!ok)
        {
            Assert.Throws<DomainException>(Make);
            return;
        }
        var q = Make();
        Assert.Equal(passage, q.PassageId);
        Assert.Equal(category, q.ReportingCategory);
    }
}
