using K12.Application.Abstractions;
using K12.Domain.Common;
using K12.Domain.Curriculum;

namespace K12.Application.ContentImport.ImportCourse;

public sealed record ImportCategory(int Number, string Name);

public sealed record ImportSkill(string Code, string Description, int? Category, string? Designation, string? LimitsJson);

/// <summary>Items per reporting category on the full practice test.</summary>
public sealed record ImportCategoryShare(int Category, int Items);

/// <summary>The real statewide test's shape: item count, time limit (empty when untimed) and category mix.</summary>
public sealed record ImportPracticeTest(int Items, int? Minutes, IReadOnlyList<ImportCategoryShare>? Mix);

/// <summary>A course's curriculum as the content pipeline publishes it. Language defaults to English.</summary>
public sealed record ImportCourseCommand(
    string Jurisdiction, string Subject, string Code, string Name,
    string FrameworkCode, string FrameworkName,
    IReadOnlyList<ImportCategory> Categories, IReadOnlyList<ImportSkill> Skills, string? JurisdictionName = null,
    string? Kind = null, int? TypicalGrade = null, string? Language = null, ImportPracticeTest? PracticeTest = null);

public sealed record ImportCourseResponse(Guid CourseId, bool CourseCreated, int SkillsCreated, int SkillsUpdated);

/// <summary>
/// Creates or updates a course and its skills. Safe to run again: skills are matched by code,
/// and skills missing from the import are left alone (questions may still point at them).
/// </summary>
public sealed class ImportCourseHandler(ICourseRepository courses, ISkillRepository skills, IUnitOfWork unitOfWork)
    : ICommandHandler<ImportCourseCommand, ImportCourseResponse>
{
    public async Task<Result<ImportCourseResponse>> HandleAsync(ImportCourseCommand command, CancellationToken ct = default)
    {
        var duplicate = command.Skills.GroupBy(s => s.Code.Trim(), StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
            return Error.Validation("import.duplicate_skill", $"Skill {duplicate.Key} appears more than once.");

        var categories = command.Categories.Select(c => new ReportingCategory(c.Number, c.Name.Trim())).ToList();
        try
        {
            var course = await courses.FindAsync(command.Jurisdiction, command.Subject, command.Code, command.Language ?? Course.DefaultLanguage, ct);
            var created = course is null;
            if (course is null)
            {
                course = Course.Create(command.Jurisdiction, command.Subject, command.Code, command.Name,
                    command.FrameworkCode, command.FrameworkName, categories, command.Language ?? Course.DefaultLanguage);
                await courses.AddAsync(course, ct);
            }
            else
            {
                course.Describe(command.Name, command.FrameworkCode, command.FrameworkName, categories);
            }

            if (command.JurisdictionName is not null) course.NameJurisdiction(command.JurisdictionName);
            if (command.Kind is not null || command.TypicalGrade is not null)
            {
                if (!Enum.TryParse<CourseKind>(command.Kind ?? nameof(CourseKind.Grade), ignoreCase: true, out var kind)
                    || !Enum.IsDefined(kind))
                    return Error.Validation("import.invalid_course", $"Kind '{command.Kind}' is not one of: grade, course.");
                course.Place(kind, command.TypicalGrade);
            }

            course.DescribePracticeTest(command.PracticeTest is { } test
                ? new PracticeTestBlueprint(test.Items, test.Minutes,
                    (test.Mix ?? []).Select(m => new CategoryShare(m.Category, m.Items)).ToList())
                : null);

            var existing = (await skills.ListByCourseAsync(course.Id, ct))
                .ToDictionary(s => s.Code, StringComparer.OrdinalIgnoreCase);
            int added = 0, updated = 0;
            foreach (var s in command.Skills)
            {
                if (existing.TryGetValue(s.Code.Trim(), out var skill))
                {
                    skill.Redefine(course, s.Description, s.Category, s.Designation, s.LimitsJson ?? "{}");
                    updated++;
                }
                else
                {
                    await skills.AddAsync(Skill.Define(course, s.Code, s.Description, s.Category, s.Designation, s.LimitsJson ?? "{}"), ct);
                    added++;
                }
            }

            await unitOfWork.SaveChangesAsync(ct);
            return new ImportCourseResponse(course.Id.Value, created, added, updated);
        }
        catch (DomainException e)
        {
            return Error.Validation("import.invalid_course", e.Message);
        }
    }
}
