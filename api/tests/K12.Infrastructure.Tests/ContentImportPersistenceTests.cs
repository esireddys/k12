using K12.Domain.Content;
using K12.Domain.Content.Answers;
using K12.Domain.Curriculum;
using K12.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K12.Infrastructure.Tests;

/// <summary>Round-trips courses, skills and exam forms through the EF Core mappings (SQLite in memory).</summary>
public sealed class ContentImportPersistenceTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public ContentImportPersistenceTests()
    {
        _connection.Open();
        using var db = NewContext();
        db.Database.EnsureCreated();
    }

    private K12DbContext NewContext() =>
        new(new DbContextOptionsBuilder<K12DbContext>().UseSqlite(_connection).Options);

    [Fact]
    public async Task A_course_with_skills_and_a_form_loads_back()
    {
        var course = Course.Create("TX", "math", "G3", "Grade 3", "FW", "Framework",
            [new ReportingCategory(1, "Numbers"), new ReportingCategory(2, "Computation")]);
        var skill = Skill.Define(course, "3.4(K)", "Solve", 2, "readiness", """{"max":100}""");
        var question = Question.Draft([skill.Id], "choice", """{"stem":"?"}""",
            new AnswerSpec { Kind = AnswerKind.Choice, Value = "H" }, Difficulty.OnLevel, QuestionOrigin.Official);
        var form = ExamForm.Create(course.Id, "EXAM", 2022, "spring", "https://example.org/t.pdf");
        form.AddItem(1, question.Id);

        await using (var db = NewContext())
        {
            await new EfCourseRepository(db).AddAsync(course);
            await new EfSkillRepository(db).AddAsync(skill);
            await new EfQuestionRepository(db).AddAsync(question);
            await new EfExamFormRepository(db).AddAsync(form);
            await db.SaveChangesAsync();
        }

        await using (var db = NewContext())
        {
            var loaded = await new EfCourseRepository(db).FindAsync("tx", "Math", "g3");
            Assert.NotNull(loaded);
            Assert.Equal(["Numbers", "Computation"], loaded.Categories.Select(c => c.Name));

            var skills = await new EfSkillRepository(db).ListByCourseAsync(loaded.Id);
            Assert.Equal(("3.4(K)", 2, "readiness"), (skills.Single().Code, skills.Single().CategoryNumber, skills.Single().Designation));

            var loadedForm = await new EfExamFormRepository(db).FindAsync(loaded.Id, "EXAM", 2022, "Spring");
            Assert.NotNull(loadedForm);
            Assert.Equal(question.Id, Assert.Single(loadedForm.Items).QuestionId);
        }
    }

    [Fact]
    public async Task A_course_keeps_its_kind_and_typical_grade()
    {
        var algebra = Course.Create("TX", "math", "A1", "Algebra I", "FW", "Framework", [new ReportingCategory(1, "Numbers")]);
        algebra.Place(CourseKind.Course, 9);

        await using (var db = NewContext())
        {
            await new EfCourseRepository(db).AddAsync(algebra);
            await db.SaveChangesAsync();
        }

        await using (var db = NewContext())
        {
            var loaded = await new EfCourseRepository(db).FindAsync("TX", "math", "A1");
            Assert.Equal((CourseKind.Course, (int?)9), (loaded!.Kind, loaded.TypicalGrade));
        }
    }

    public void Dispose() => _connection.Dispose();
}
