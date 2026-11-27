using K12.Domain.Common;
using K12.Domain.Curriculum;

namespace K12.Domain.Tests.Curriculum;

public class CourseAndSkillTests
{
    private static Course Grade3() => Course.Create(" tx ", "Math", "g3", "Grade 3", "FW", "Framework",
        [new ReportingCategory(2, "Computation"), new ReportingCategory(1, "Numbers")]);

    [Fact]
    public void A_course_normalizes_its_key_and_orders_categories()
    {
        var course = Grade3();
        Assert.Equal(("TX", "math", "G3"), (course.Jurisdiction, course.Subject, course.Code));
        Assert.Equal([1, 2], course.Categories.Select(c => c.Number));
    }

    [Theory]
    [InlineData("", "Grade 3")]
    [InlineData("G3", " ")]
    public void A_course_needs_a_code_and_name(string code, string name) =>
        Assert.Throws<DomainException>(() => Course.Create("TX", "math", code, name, "FW", "Framework", []));

    [Fact]
    public void A_course_is_a_grade_until_told_otherwise()
    {
        var course = Grade3();
        Assert.Equal((CourseKind.Grade, (int?)null), (course.Kind, course.TypicalGrade));
    }

    [Theory]
    [InlineData(CourseKind.Grade, 3)]
    [InlineData(CourseKind.Grade, 0)]
    [InlineData(CourseKind.Course, 9)]
    [InlineData(CourseKind.Course, null)]
    [InlineData(CourseKind.Course, 12)]
    public void A_course_can_be_placed_in_school(CourseKind kind, int? grade)
    {
        var course = Grade3();
        course.Place(kind, grade);
        Assert.Equal((kind, grade), (course.Kind, course.TypicalGrade));
    }

    [Theory]
    [InlineData(CourseKind.Grade, null)]
    [InlineData(CourseKind.Course, -1)]
    [InlineData(CourseKind.Course, 13)]
    [InlineData((CourseKind)7, 9)]
    public void A_course_cannot_be_placed_outside_school(CourseKind kind, int? grade) =>
        Assert.Throws<DomainException>(() => Grade3().Place(kind, grade));

    [Fact]
    public void Category_numbers_must_be_unique() =>
        Assert.Throws<DomainException>(() => Grade3().Describe("Grade 3", "FW", "Framework",
            [new ReportingCategory(1, "A"), new ReportingCategory(1, "B")]));

    [Fact]
    public void A_skill_must_name_a_category_the_course_has()
    {
        var course = Grade3();
        Assert.Throws<DomainException>(() => Skill.Define(course, "3.4(K)", "Solve", 9, "readiness", "{}"));
    }

    [Fact]
    public void Redefining_a_skill_updates_it_in_place()
    {
        var course = Grade3();
        var skill = Skill.Define(course, " 3.4(K) ", "Old", 2, "Readiness", "");

        skill.Redefine(course, "New", 1, null, """{"max":100}""");

        Assert.Equal("3.4(K)", skill.Code);
        Assert.Equal(("New", 1, (string?)null, """{"max":100}"""),
            (skill.Description, skill.CategoryNumber, skill.Designation, skill.LimitsJson));
    }

    [Fact]
    public void A_skill_cannot_move_to_another_course()
    {
        var skill = Skill.Define(Grade3(), "3.4(K)", "Solve", 2, null, "{}");
        Assert.Throws<DomainException>(() => skill.Redefine(Grade3(), "Solve", 2, null, "{}"));
    }
}
