using K12.Domain.Common;
using K12.Domain.Content;
using K12.Domain.Curriculum;

namespace K12.Domain.Tests.Content;

public class ExamFormTests
{
    private static ExamForm Form() => ExamForm.Create(CourseId.New(), "EXAM", 2021, "Spring", "https://example.org/t.pdf");

    [Fact]
    public void Items_are_kept_in_number_order()
    {
        var form = Form();
        form.AddItem(2, QuestionId.New());
        form.AddItem(1, QuestionId.New());
        Assert.Equal([1, 2], form.Items.Select(i => i.Number));
        Assert.Equal("spring", form.Season);
    }

    [Fact]
    public void A_number_is_used_once()
    {
        var form = Form();
        form.AddItem(1, QuestionId.New());
        Assert.Throws<DomainException>(() => form.AddItem(1, QuestionId.New()));
    }

    [Fact]
    public void A_question_fills_one_slot()
    {
        var form = Form();
        var q = QuestionId.New();
        form.AddItem(1, q);
        Assert.Throws<DomainException>(() => form.AddItem(2, q));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Numbers_start_at_one(int number) =>
        Assert.Throws<DomainException>(() => Form().AddItem(number, QuestionId.New()));

    [Theory]
    [InlineData("", 2021, "spring")]
    [InlineData("EXAM", 1066, "spring")]
    [InlineData("EXAM", 2021, " ")]
    public void A_form_needs_an_exam_a_plausible_year_and_a_season(string exam, int year, string season) =>
        Assert.Throws<DomainException>(() => ExamForm.Create(CourseId.New(), exam, year, season, ""));
}
