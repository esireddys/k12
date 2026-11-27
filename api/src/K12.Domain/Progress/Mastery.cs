namespace K12.Domain.Progress;

/// <summary>How well a kid knows one skill or one reporting category, from their recent answers on it.</summary>
public enum MasteryLevel
{
    /// <summary>No answers yet.</summary>
    NotStarted,
    /// <summary>Fewer than half of the recent answers were right.</summary>
    Learning,
    /// <summary>Getting there: answering, but not yet steadily right.</summary>
    Practicing,
    /// <summary>Enough answers, and nearly all of the latest ones right (see <see cref="MasteryRule"/>).</summary>
    Mastered,
}

/// <summary>
/// When recent answers count as mastered: at least <paramref name="MinAnswers"/> answers, and at least
/// <paramref name="RightToMaster"/> of the last <paramref name="Judged"/> right. Only the latest
/// <paramref name="Window"/> answers are remembered.
/// </summary>
public sealed record MasteryRule(int Window, int MinAnswers, int Judged, int RightToMaster);

/// <summary>
/// The mastery rules. They look at recent answers only, so a kid who struggled at first and now answers steadily
/// right counts as having mastered it, and one who has slipped no longer does.
/// </summary>
public static class Mastery
{
    /// <summary>One skill: 4 of the last 5 right. Most skills have few released questions, so this is a light signal.</summary>
    public static readonly MasteryRule Skill = new(Window: 10, MinAnswers: 5, Judged: 5, RightToMaster: 4);

    /// <summary>
    /// One reporting category, which is how the state test reports results and the progress kids and parents see:
    /// 7 of the last 8 right. Every category in a released test has enough questions to get there.
    /// </summary>
    public static readonly MasteryRule Category = new(Window: 12, MinAnswers: 8, Judged: 8, RightToMaster: 7);

    /// <summary>How many recent answers a skill remembers.</summary>
    public static int Window => Skill.Window;

    /// <summary>The fewest answers before a skill can count as mastered.</summary>
    public static int MinAnswers => Skill.MinAnswers;

    /// <param name="recent">The latest answers, oldest first: '1' right, '0' wrong.</param>
    public static MasteryLevel Level(string recent) => Level(recent, Skill);

    /// <param name="recent">The latest answers, oldest first: '1' right, '0' wrong. At most the rule's window.</param>
    public static MasteryLevel Level(string recent, MasteryRule rule)
    {
        if (string.IsNullOrEmpty(recent)) return MasteryLevel.NotStarted;
        var judged = recent[^Math.Min(rule.Judged, recent.Length)..];
        if (recent.Length >= rule.MinAnswers && judged.Count(c => c == '1') >= rule.RightToMaster) return MasteryLevel.Mastered;
        var right = recent.Count(c => c == '1');
        return right * 2 < recent.Length ? MasteryLevel.Learning : MasteryLevel.Practicing;
    }

    /// <summary>Adds one answer to the window and drops the oldest beyond it.</summary>
    public static string Push(string recent, bool correct) => Push(recent, correct, Skill);

    public static string Push(string recent, bool correct, MasteryRule rule)
    {
        var next = (recent ?? string.Empty) + (correct ? '1' : '0');
        return next.Length > rule.Window ? next[^rule.Window..] : next;
    }
}
