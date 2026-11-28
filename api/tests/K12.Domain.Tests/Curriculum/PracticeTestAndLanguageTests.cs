using K12.Domain.Common;
using K12.Domain.Curriculum;

namespace K12.Domain.Tests.Curriculum;

public class PracticeTestAndLanguageTests
{
    private static Course Course(string subject = "math", string code = "G3", string jurisdiction = "TX", string language = "en") =>
        K12.Domain.Curriculum.Course.Create(jurisdiction, subject, code, "Grade 3", "FW", "Framework",
            [new ReportingCategory(1, "One"), new ReportingCategory(2, "Two")], language);

    public static TheoryData<int, int?, (int, int)[], string?> Blueprints => new()
    {
        { 32, 240, [(1, 20), (2, 12)], null },
        { 32, null, [], null },
        { 34, 240, [(1, 5)], null },
        { 0, 240, [], "1 to" },
        { PracticeTestBlueprint.MaxItems + 1, 240, [], "1 to" },
        { 32, 0, [], "minutes" },
        { 32, PracticeTestBlueprint.MaxMinutes + 1, [], "minutes" },
        { 32, 240, [(3, 4)], "category 3" },
        { 32, 240, [(1, 4), (1, 4)], "appears once" },
        { 32, 240, [(1, -1)], "negative" },
        { 10, 240, [(1, 6), (2, 6)], "more items" },
    };

    [Theory]
    [MemberData(nameof(Blueprints))]
    public void A_practice_test_must_fit_its_course(int items, int? minutes, (int, int)[] mix, string? error)
    {
        var course = Course();
        var blueprint = new PracticeTestBlueprint(items, minutes, mix.Select(m => new CategoryShare(m.Item1, m.Item2)).ToList());
        if (error is null)
        {
            course.DescribePracticeTest(blueprint);
            Assert.Equal(items, course.PracticeTest!.ItemCount);
        }
        else
        {
            var e = Assert.Throws<DomainException>(() => course.DescribePracticeTest(blueprint));
            Assert.Contains(error, e.Message);
            Assert.Null(course.PracticeTest);
        }
    }

    [Fact]
    public void A_practice_test_can_be_removed()
    {
        var course = Course();
        course.DescribePracticeTest(new PracticeTestBlueprint(32, 240, []));
        course.DescribePracticeTest(null);
        Assert.Null(course.PracticeTest);
    }

    [Theory]
    [InlineData(null, "en")]
    [InlineData("", "en")]
    [InlineData("EN", "en")]
    [InlineData(" es ", "es")]
    [InlineData("pt-BR", "pt-br")]
    public void Language_tags_are_normalized_and_default_to_english(string? given, string expected) =>
        Assert.Equal(expected, K12.Domain.Curriculum.Course.NormalizeLanguage(given));

    [Theory]
    [InlineData("english")]
    [InlineData("e")]
    [InlineData("en_US")]
    public void Malformed_language_tags_are_refused(string given) =>
        Assert.Throws<DomainException>(() => K12.Domain.Curriculum.Course.NormalizeLanguage(given));

    [Theory]
    [InlineData("reading", "G3", "TX", "en", true)]
    [InlineData("math", "G3", "TX", "en", true)]
    [InlineData("reading", "G4", "TX", "en", false)]
    [InlineData("reading", "G3", "NY", "en", false)]
    [InlineData("reading", "G3", "TX", "es", false)]
    public void Another_subject_in_the_same_grade_state_and_language_is_the_same_level(
        string subject, string code, string jurisdiction, string language, bool same) =>
        Assert.Equal(same, Course().IsSameLevelAs(Course(subject, code, jurisdiction, language)));

    [Fact]
    public void Courses_are_english_unless_created_otherwise()
    {
        Assert.Equal("en", Course().Language);
        Assert.Equal("es", Course(language: "ES").Language);
    }
}
