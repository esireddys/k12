using K12.Application.Abstractions;
using K12.Domain.Content;
using K12.Domain.Curriculum;

namespace K12.Application.Practice.Sets;

public sealed record CatalogSkill(string Code, string Description, int QuestionCount);

public sealed record CatalogCategory(int Number, string Name, int QuestionCount, IReadOnlyList<CatalogSkill> Skills);

/// <summary>A released test students can take in full ("Pick a year").</summary>
public sealed record CatalogForm(Guid Id, string Exam, int Year, string Season, int QuestionCount);

/// <summary>
/// One course students can pick, with what they can practice in it. Every name (jurisdiction, course, framework,
/// exam, category) is curriculum data from the import. Kind is "Grade" (Grade 3 math) or "Course" (Algebra I), and
/// TypicalGrade is the grade students usually take it in (0 is kindergarten), when the curriculum says.
/// </summary>
public sealed record CatalogCourse(
    Guid Id, string Jurisdiction, string JurisdictionName, string Subject, string Code, string Name,
    string FrameworkCode, string FrameworkName, int QuestionCount,
    IReadOnlyList<CatalogCategory> Categories, IReadOnlyList<CatalogForm> Forms,
    string Kind, int? TypicalGrade);

public sealed record PracticeCatalog(IReadOnlyList<CatalogCourse> Courses);

public sealed record GetPracticeCatalogQuery;

/// <summary>
/// Every course with counts of its Live questions by topic and by released test, in school order within each
/// jurisdiction: by typical grade, grades before courses in the same grade, then by name.
/// </summary>
public sealed class GetPracticeCatalogHandler(
    ICourseRepository courses, ISkillRepository skills, IExamFormRepository forms, IQuestionRepository questions)
    : IQueryHandler<GetPracticeCatalogQuery, PracticeCatalog>
{
    public async Task<Result<PracticeCatalog>> HandleAsync(GetPracticeCatalogQuery query, CancellationToken ct = default)
    {
        var live = await questions.ListByStatusAsync(QuestionStatus.Live, int.MaxValue, ct);
        var liveIds = live.Select(q => q.Id).ToHashSet();
        var perSkill = live.SelectMany(q => q.SkillIds.Distinct()).GroupBy(id => id).ToDictionary(g => g.Key, g => g.Count());

        var result = new List<CatalogCourse>();
        var ordered = (await courses.ListAllAsync(ct))
            .OrderBy(c => c.Jurisdiction, StringComparer.Ordinal)
            .ThenBy(c => c.TypicalGrade ?? int.MaxValue)
            .ThenBy(c => c.Kind)
            .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.Subject, StringComparer.Ordinal);
        foreach (var course in ordered)
        {
            var courseSkills = await skills.ListByCourseAsync(course.Id, ct);
            var skillIds = courseSkills.Select(s => s.Id).ToHashSet();
            var courseQuestions = live.Where(q => q.SkillIds.Any(skillIds.Contains)).ToList();

            var categories = course.Categories.Select(cat =>
            {
                var catSkills = courseSkills.Where(s => s.CategoryNumber == cat.Number).OrderBy(s => s.Code, StringComparer.Ordinal).ToList();
                var catSkillIds = catSkills.Select(s => s.Id).ToHashSet();
                return new CatalogCategory(
                    cat.Number, cat.Name,
                    courseQuestions.Count(q => q.SkillIds.Any(catSkillIds.Contains)),
                    catSkills.Select(s => new CatalogSkill(s.Code, s.Description, perSkill.GetValueOrDefault(s.Id))).ToList());
            }).ToList();

            var courseForms = (await forms.ListByCourseAsync(course.Id, ct))
                .Select(f => new CatalogForm(f.Id.Value, f.Exam, f.Year, f.Season, f.Items.Count(i => liveIds.Contains(i.QuestionId))))
                .Where(f => f.QuestionCount > 0)
                .ToList();

            result.Add(new CatalogCourse(
                course.Id.Value, course.Jurisdiction, course.JurisdictionName, course.Subject, course.Code, course.Name,
                course.FrameworkCode, course.FrameworkName, courseQuestions.Count, categories, courseForms,
                course.Kind.ToString(), course.TypicalGrade));
        }

        return new PracticeCatalog(result);
    }
}
