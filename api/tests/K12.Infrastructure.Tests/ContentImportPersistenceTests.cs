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

    [Fact]
    public async Task A_reading_course_keeps_its_language_practice_test_and_passages()
    {
        var reading = Course.Create("TX", "reading", "G3", "Grade 3", "FW", "Framework",
            [new ReportingCategory(1, "Across"), new ReportingCategory(2, "Literary")]);
        reading.DescribePracticeTest(new PracticeTestBlueprint(34, 240, [new CategoryShare(1, 5), new CategoryShare(2, 15)]));
        var spanish = Course.Create("TX", "reading", "G3", "Grado 3", "FW", "Framework", [new ReportingCategory(1, "A")], "es");
        var skill = Skill.Define(reading, "3.6(F)", "Inferences", null, "readiness", "{}");
        var passage = Passage.Create(reading.Id, "The Unwelcome Neighbor", """{"pages":["p1-1.png","p1-2.png"]}""", QuestionOrigin.Official);
        var question = Question.Draft([skill.Id], "choice", """{"stem":"?"}""",
            new AnswerSpec { Kind = AnswerKind.Choice, Value = "D" }, Difficulty.OnLevel, QuestionOrigin.Official,
            passageId: passage.Id, reportingCategory: 2);

        await using (var db = NewContext())
        {
            await new EfCourseRepository(db).AddAsync(reading);
            await new EfCourseRepository(db).AddAsync(spanish);
            await new EfSkillRepository(db).AddAsync(skill);
            await new EfPassageRepository(db).AddAsync(passage);
            await new EfQuestionRepository(db).AddAsync(question);
            await db.SaveChangesAsync();
        }

        await using (var db = NewContext())
        {
            var loaded = await new EfCourseRepository(db).FindAsync("TX", "reading", "G3");
            Assert.Equal(("en", "Grade 3"), (loaded!.Language, loaded.Name));
            Assert.Equal(34, loaded.PracticeTest!.ItemCount);
            Assert.Equal([5, 15], loaded.PracticeTest.Mix.Select(m => m.Items));
            Assert.Equal("Grado 3", (await new EfCourseRepository(db).FindAsync("TX", "reading", "G3", "ES"))!.Name);

            var loadedPassage = Assert.Single(await new EfPassageRepository(db).ListByIdsAsync([passage.Id]));
            Assert.Equal("The Unwelcome Neighbor", loadedPassage.Title);
            var loadedQuestion = await new EfQuestionRepository(db).GetAsync(question.Id);
            Assert.Equal((passage.Id, 2), (loadedQuestion!.PassageId!.Value, loadedQuestion.ReportingCategory!.Value));
            Assert.Null((await new EfSkillRepository(db).ListByCourseAsync(reading.Id)).Single().CategoryNumber);
        }
    }

    public void Dispose() => _connection.Dispose();
}
