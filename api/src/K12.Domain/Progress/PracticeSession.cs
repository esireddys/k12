using K12.Domain.Accounts;
using K12.Domain.Common;
using K12.Domain.Content;
using K12.Domain.Curriculum;

namespace K12.Domain.Progress;

/// <summary>How a set was taken: practice shows each answer right away; an exam shows nothing until the end.</summary>
public enum PracticeMode { Practice, Exam }

public readonly record struct PracticeSessionId(Guid Value)
{
    public static PracticeSessionId New() => new(Guid.NewGuid());
}

/// <summary>One answer in a session, as it was checked.</summary>
public sealed class SessionAnswer : Entity<QuestionId>
{
    public const int MaxResponseLength = 40;

    /// <summary>For the persistence layer only.</summary>
    private SessionAnswer() : base(default) => Response = string.Empty;

    internal SessionAnswer(QuestionId questionId, SkillId? skillId, string response, bool isCorrect, DateTimeOffset at) : base(questionId)
    {
        SkillId = skillId;
        var trimmed = (response ?? string.Empty).Trim();
        Response = trimmed.Length > MaxResponseLength ? trimmed[..MaxResponseLength] : trimmed;
        IsCorrect = isCorrect;
        AnsweredAt = at;
    }

    public QuestionId QuestionId => Id;
    public SkillId? SkillId { get; private set; }

    /// <summary>What the kid typed or picked, empty when they left it blank.</summary>
    public string Response { get; private set; }

    public bool Answered => Response.Length > 0;
    public bool IsCorrect { get; private set; }
    public DateTimeOffset AnsweredAt { get; private set; }
}

public sealed record PracticeSessionFinished(PracticeSessionId SessionId, KidProfileId KidId, DateTimeOffset OccurredAt) : IDomainEvent;

/// <summary>
/// One set a kid took: a topic, a mixed set, a year's test or their mistakes, with every answer they gave.
/// This is the kid's attempt history. Only the first answer to each question counts, so trying again after
/// seeing the key does not raise the score.
/// </summary>
public sealed class PracticeSession : AggregateRoot<PracticeSessionId>
{
    public const int MaxTitleLength = 80;
    public const int MaxAnswers = 100;

    private readonly List<SessionAnswer> _answers = [];

    /// <summary>For the persistence layer only.</summary>
    private PracticeSession() : base(default)
    {
        Title = string.Empty;
        Source = string.Empty;
    }

    private PracticeSession(
        PracticeSessionId id, KidProfileId kidId, CourseId courseId, PracticeMode mode, string source, string title, DateTimeOffset at)
        : base(id)
    {
        if (!Enum.IsDefined(mode)) throw new DomainException("Unknown kind of session.");
        KidId = kidId;
        CourseId = courseId;
        Mode = mode;
        Source = Clean(source, 20, "practice");
        Title = Clean(title, MaxTitleLength, "Practice");
        StartedAt = at;
        LastActiveAt = at;
    }

    public KidProfileId KidId { get; private set; }
    public CourseId CourseId { get; private set; }
    public PracticeMode Mode { get; private set; }

    /// <summary>Where the questions came from, for example "Topic" or "Year". Shown to the parent, never used in rules.</summary>
    public string Source { get; private set; }

    /// <summary>What the set was called on screen, for example a topic's name or "2022". From curriculum data.</summary>
    public string Title { get; private set; }

    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset LastActiveAt { get; private set; }
    public DateTimeOffset? FinishedAt { get; private set; }
    public IReadOnlyList<SessionAnswer> Answers => _answers;

    public bool IsFinished => FinishedAt is not null;
    public int Correct => _answers.Count(a => a.IsCorrect);
    public int Total => _answers.Count;

    /// <summary>Time spent answering, as the device measured it, capped per answer.</summary>
    public int Seconds { get; private set; }

    /// <summary>Whole minutes spent, at least one once anything was answered.</summary>
    public int Minutes => _answers.Count == 0 ? 0 : Math.Max(1, (int)Math.Round(Seconds / 60.0));

    /// <param name="id">Chosen by the device when the set starts, so retries of the same request land on one session.</param>
    public static PracticeSession Start(
        PracticeSessionId id, KidProfileId kidId, CourseId courseId, PracticeMode mode, string source, string title, DateTimeOffset at) =>
        new(id, kidId, courseId, mode, source, title, at);

    /// <summary>Adds time the device measured for answers just given, capped so a tablet left open does not count.</summary>
    public void AddTime(int seconds, int answers)
    {
        if (answers <= 0) return;
        Seconds += Math.Clamp(seconds, 0, KidProgress.MaxSecondsPerAnswer * answers);
    }

    /// <summary>
    /// Adds an answer. Returns false and changes nothing when the question was already answered in this session.
    /// </summary>
    public bool Answer(QuestionId questionId, SkillId? skillId, string? response, bool isCorrect, DateTimeOffset at)
    {
        if (IsFinished) throw new DomainException("This set is finished. Start a new one.");
        if (_answers.Any(a => a.QuestionId == questionId)) return false;
        if (_answers.Count >= MaxAnswers) throw new DomainException($"A set can have at most {MaxAnswers} questions.");
        _answers.Add(new SessionAnswer(questionId, skillId, response ?? string.Empty, isCorrect, at));
        LastActiveAt = at;
        return true;
    }

    /// <summary>Ends the session. Finishing twice is harmless: the first finish counts.</summary>
    public void Finish(DateTimeOffset at)
    {
        if (IsFinished) return;
        FinishedAt = at;
        Raise(new PracticeSessionFinished(Id, KidId, at));
    }

    private static string Clean(string? value, int max, string fallback)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length == 0) return fallback;
        return trimmed.Length > max ? trimmed[..max] : trimmed;
    }
}
