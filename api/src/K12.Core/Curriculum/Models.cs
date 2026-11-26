using K12.Core.Answers;

namespace K12.Core.Curriculum;

// Content is data: a new state, grade, exam or skill is a new row, not new code.
// These records mirror the data model in the architecture doc; EF Core mappings come next.

/// <summary>A state or other education authority, for example TX.</summary>
public sealed record Jurisdiction(string Code, string Name);

/// <summary>A standards framework, for example TEKS Mathematics.</summary>
public sealed record Framework(Guid Id, string JurisdictionCode, string Subject, string Name, string Version);

/// <summary>A grade or course, for example G3 or ALG1.</summary>
public sealed record Level(Guid Id, Guid FrameworkId, string Code, string Label);

/// <summary>A reporting category, for example STAAR Grade 3 category 1.</summary>
public sealed record Category(Guid Id, Guid LevelId, string Code, string Name, int Order);

/// <summary>A tested skill, for example TEKS 3.3B. Limits hold the per-skill rules the AI reviewer checks.</summary>
public sealed record Skill(Guid Id, Guid CategoryId, string Code, string Description, string LimitsJson);

public enum QuestionOrigin { Official, Generated }

public enum QuestionStatus { Draft, InAiReview, AwaitingOwner, Live, Rejected, Retired }

public enum Difficulty { Easier, OnLevel, Harder }

/// <summary>One versioned question. Content is JSON (text blocks, figure references) so renderers stay generic.</summary>
public sealed record Question(
    Guid Id,
    int Version,
    IReadOnlyList<Guid> SkillIds,
    string ItemType,
    string ContentJson,
    AnswerSpec Answer,
    Difficulty Difficulty,
    QuestionOrigin Origin,
    Guid? ParentQuestionId,
    QuestionStatus Status);
