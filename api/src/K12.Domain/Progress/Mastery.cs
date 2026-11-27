namespace K12.Domain.Progress;

/// <summary>How well a kid knows one skill, from their recent answers on it.</summary>
public enum MasteryLevel
{
    /// <summary>No answers on the skill yet.</summary>
    NotStarted,
    /// <summary>Fewer than half of the recent answers were right.</summary>
    Learning,
    /// <summary>Getting there: answering, but not yet steadily right.</summary>
    Practicing,
    /// <summary>At least <see cref="Mastery.MinAnswers"/> answers and at least 4 of the last 5 right.</summary>
    Mastered,
}

/// <summary>
/// The mastery rule. It looks at recent answers only, so a kid who struggled at first and now answers steadily
/// right counts as having mastered the skill, and one who has slipped no longer does.
/// </summary>
public static class Mastery
{
    /// <summary>How many recent answers a skill remembers.</summary>
    public const int Window = 10;

    /// <summary>The fewest answers before a skill can count as mastered.</summary>
    public const int MinAnswers = 5;

    /// <param name="recent">The latest answers, oldest first: '1' right, '0' wrong. At most <see cref="Window"/>.</param>
    public static MasteryLevel Level(string recent)
    {
        if (string.IsNullOrEmpty(recent)) return MasteryLevel.NotStarted;
        var lastFive = recent[^Math.Min(5, recent.Length)..];
        if (recent.Length >= MinAnswers && lastFive.Count(c => c == '1') >= 4) return MasteryLevel.Mastered;
        var right = recent.Count(c => c == '1');
        return right * 2 < recent.Length ? MasteryLevel.Learning : MasteryLevel.Practicing;
    }

    /// <summary>Adds one answer to the window and drops the oldest beyond it.</summary>
    public static string Push(string recent, bool correct)
    {
        var next = (recent ?? string.Empty) + (correct ? '1' : '0');
        return next.Length > Window ? next[^Window..] : next;
    }
}
