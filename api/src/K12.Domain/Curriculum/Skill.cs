using K12.Domain.Common;

namespace K12.Domain.Curriculum;

public readonly record struct SkillId(Guid Value)
{
    public static SkillId New() => new(Guid.NewGuid());
}

/// <summary>
/// A tested skill within a framework, for example code "3.3B" in Texas Grade 3 math.
/// The framework and its name come from data; nothing here assumes a particular state or exam.
/// </summary>
public sealed class Skill : AggregateRoot<SkillId>
{
    public Skill(SkillId id, string code, string description, string limitsJson) : base(id)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new DomainException("A skill needs a code.");
        Code = code.Trim();
        Description = description;
        LimitsJson = limitsJson;
    }

    public string Code { get; }
    public string Description { get; private set; }

    /// <summary>The per-skill rule sheet the AI reviewer checks, for example allowed denominators.</summary>
    public string LimitsJson { get; private set; }
}
