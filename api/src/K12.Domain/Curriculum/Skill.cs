using K12.Domain.Common;

namespace K12.Domain.Curriculum;

public readonly record struct SkillId(Guid Value)
{
    public static SkillId New() => new(Guid.NewGuid());
}

/// <summary>
/// A tested skill within a course, for example code "3.3(F)" in Texas Grade 3 math.
/// The framework and its name come from data; nothing here assumes a particular state or exam.
/// </summary>
public sealed class Skill : AggregateRoot<SkillId>
{
    /// <summary>For the persistence layer only.</summary>
    private Skill() : base(default)
    {
        Code = string.Empty;
        Description = string.Empty;
        LimitsJson = string.Empty;
    }

    private Skill(SkillId id, CourseId courseId, string code) : base(id)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new DomainException("A skill needs a code.");
        CourseId = courseId;
        Code = code.Trim();
        Description = string.Empty;
        LimitsJson = "{}";
    }

    public CourseId CourseId { get; }
    public string Code { get; }
    public string Description { get; private set; }

    /// <summary>The reporting category this skill belongs to within its course.</summary>
    public int? CategoryNumber { get; private set; }

    /// <summary>How heavily the framework weights the skill, for example "readiness" or "supporting". Data, not logic.</summary>
    public string? Designation { get; private set; }

    /// <summary>The per-skill rule sheet the AI reviewer checks, for example allowed denominators.</summary>
    public string LimitsJson { get; private set; }

    /// <summary>Adds a skill to a course. The category must be one the course defines.</summary>
    public static Skill Define(
        Course course, string code, string description, int? categoryNumber, string? designation, string limitsJson)
    {
        var skill = new Skill(SkillId.New(), course.Id, code);
        skill.Redefine(course, description, categoryNumber, designation, limitsJson);
        return skill;
    }

    public void Redefine(Course course, string description, int? categoryNumber, string? designation, string limitsJson)
    {
        if (course.Id != CourseId) throw new DomainException($"Skill {Code} belongs to a different course.");
        if (categoryNumber is { } n && !course.HasCategory(n))
            throw new DomainException($"Skill {Code} names reporting category {n}, which the course does not have.");
        Description = description.Trim();
        CategoryNumber = categoryNumber;
        Designation = string.IsNullOrWhiteSpace(designation) ? null : designation.Trim().ToLowerInvariant();
        LimitsJson = string.IsNullOrWhiteSpace(limitsJson) ? "{}" : limitsJson;
    }
}
