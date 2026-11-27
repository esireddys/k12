using K12.Domain.Accounts;
using K12.Domain.Common;
using K12.Domain.Content;
using K12.Domain.Curriculum;

namespace K12.Domain.Progress;

/// <summary>One answer to record against a kid's progress.</summary>
/// <param name="SkillId">The question's skill, when it has one in the kid's course.</param>
/// <param name="Answered">False for a blank answer, which counts as wrong.</param>
public sealed record AnswerFact(QuestionId QuestionId, SkillId? SkillId, bool Answered, bool IsCorrect);

/// <summary>How a kid is doing on one skill.</summary>
public sealed class SkillProgress : Entity<SkillId>
{
    /// <summary>For the persistence layer only.</summary>
    private SkillProgress() : base(default) => Recent = string.Empty;

    internal SkillProgress(SkillId skillId) : base(skillId) => Recent = string.Empty;

    public SkillId SkillId => Id;
    public int Answered { get; private set; }
    public int Correct { get; private set; }

    /// <summary>The latest answers, oldest first ('1' right, '0' wrong), for <see cref="Mastery"/>.</summary>
    public string Recent { get; private set; }

    public DateTimeOffset LastAnsweredAt { get; private set; }

    public MasteryLevel Level => Mastery.Level(Recent);

    internal void Record(bool correct, DateTimeOffset at)
    {
        Answered++;
        if (correct) Correct++;
        Recent = Mastery.Push(Recent, correct);
        LastAnsweredAt = at;
    }
}

/// <summary>
/// A question the kid got wrong. It stays on their "Fix my mistakes" list until they answer it right.
/// </summary>
public sealed class Mistake : Entity<QuestionId>
{
    /// <summary>For the persistence layer only.</summary>
    private Mistake() : base(default) { }

    internal Mistake(QuestionId questionId, SkillId? skillId, DateTimeOffset at) : base(questionId)
    {
        SkillId = skillId;
        FirstMissedAt = at;
        Miss(at);
    }

    public QuestionId QuestionId => Id;
    public SkillId? SkillId { get; private set; }
    public DateTimeOffset FirstMissedAt { get; private set; }
    public DateTimeOffset LastMissedAt { get; private set; }
    public int TimesMissed { get; private set; }

    /// <summary>When the kid answered it right after missing it. A fixed mistake leaves the list.</summary>
    public DateTimeOffset? FixedAt { get; private set; }

    public bool IsOpen => FixedAt is null;

    internal void Miss(DateTimeOffset at)
    {
        TimesMissed++;
        LastMissedAt = at;
        FixedAt = null;
    }

    internal void Fix(DateTimeOffset at) => FixedAt ??= at;
}

/// <summary>One day a kid practiced, on the kid's own calendar (their device's date, not the server's).</summary>
public sealed class PracticeDay : Entity<DateOnly>
{
    /// <summary>For the persistence layer only.</summary>
    private PracticeDay() : base(default) { }

    internal PracticeDay(DateOnly date) : base(date) { }

    public DateOnly Date => Id;
    public int Answered { get; private set; }
    public int Correct { get; private set; }

    /// <summary>Time spent answering, as the device measured it, capped per batch.</summary>
    public int Seconds { get; private set; }

    internal void Record(int answered, int correct, int seconds)
    {
        Answered += answered;
        Correct += correct;
        Seconds += seconds;
    }
}

/// <summary>
/// Everything that sums up how one kid is doing: mastery per skill, the mistakes still to fix, and the days they
/// practiced (for streaks and the parent's weekly view). One per kid; the answers themselves live on
/// <see cref="PracticeSession"/>. Guests have none: nothing a guest does is saved.
/// </summary>
public sealed class KidProgress : AggregateRoot<KidProfileId>
{
    /// <summary>A device may report its date at most a day off the server's, for time zones; more is a wrong clock.</summary>
    public static readonly TimeSpan MaxClockSkew = TimeSpan.FromHours(26);

    /// <summary>The most seconds one batch of answers can add, so a tablet left open overnight does not count.</summary>
    public const int MaxSecondsPerAnswer = 600;

    private readonly List<SkillProgress> _skills = [];
    private readonly List<Mistake> _mistakes = [];
    private readonly List<PracticeDay> _days = [];

    /// <summary>For the persistence layer only.</summary>
    private KidProgress() : base(default) { }

    private KidProgress(KidProfileId kidId) : base(kidId) { }

    public static KidProgress Start(KidProfileId kidId) => new(kidId);

    public KidProfileId KidId => Id;
    public IReadOnlyList<SkillProgress> Skills => _skills;
    public IReadOnlyList<Mistake> Mistakes => _mistakes;
    public IReadOnlyList<PracticeDay> Days => _days;

    public IEnumerable<Mistake> OpenMistakes => _mistakes.Where(m => m.IsOpen);

    public int TotalAnswered => _days.Sum(d => d.Answered);
    public int TotalCorrect => _days.Sum(d => d.Correct);

    /// <summary>
    /// Records answers the kid just gave. A wrong answer puts the question on the mistakes list; a right answer
    /// to a question on the list takes it off.
    /// </summary>
    /// <param name="localDate">The kid's own date, from their device.</param>
    /// <param name="seconds">Time spent on these answers, as the device measured it.</param>
    public void Record(IReadOnlyCollection<AnswerFact> answers, DateOnly localDate, int seconds, DateTimeOffset at)
    {
        if (answers.Count == 0) return;
        var serverDate = DateOnly.FromDateTime(at.UtcDateTime);
        var skew = Math.Abs(localDate.DayNumber - serverDate.DayNumber);
        if (skew > 1) throw new DomainException("The device's date looks wrong. Check the date on this device.");

        foreach (var answer in answers)
        {
            if (answer.SkillId is { } skillId) Skill(skillId).Record(answer.IsCorrect, at);

            var mistake = _mistakes.FirstOrDefault(m => m.QuestionId == answer.QuestionId);
            if (answer.IsCorrect) mistake?.Fix(at);
            else if (mistake is null) _mistakes.Add(new Mistake(answer.QuestionId, answer.SkillId, at));
            else mistake.Miss(at);
        }

        var cappedSeconds = Math.Clamp(seconds, 0, MaxSecondsPerAnswer * answers.Count);
        Day(localDate).Record(answers.Count, answers.Count(a => a.IsCorrect), cappedSeconds);
    }

    /// <summary>
    /// Days in a row with practice, ending today or yesterday: a streak is not broken until a whole day passes
    /// with no practice.
    /// </summary>
    public int CurrentStreak(DateOnly today)
    {
        var practiced = _days.Where(d => d.Answered > 0).Select(d => d.Date).ToHashSet();
        var day = practiced.Contains(today) ? today : today.AddDays(-1);
        var streak = 0;
        while (practiced.Contains(day))
        {
            streak++;
            day = day.AddDays(-1);
        }
        return streak;
    }

    /// <summary>The longest run of days in a row with practice.</summary>
    public int BestStreak()
    {
        var best = 0;
        var run = 0;
        DateOnly? previous = null;
        foreach (var date in _days.Where(d => d.Answered > 0).Select(d => d.Date).Order())
        {
            run = previous is { } p && p.AddDays(1) == date ? run + 1 : 1;
            best = Math.Max(best, run);
            previous = date;
        }
        return best;
    }

    /// <summary>The practice days from <paramref name="from"/> to <paramref name="to"/>, both included.</summary>
    public IReadOnlyList<PracticeDay> DaysBetween(DateOnly from, DateOnly to) =>
        _days.Where(d => d.Date >= from && d.Date <= to).OrderBy(d => d.Date).ToList();

    private SkillProgress Skill(SkillId skillId)
    {
        var skill = _skills.FirstOrDefault(s => s.SkillId == skillId);
        if (skill is not null) return skill;
        skill = new SkillProgress(skillId);
        _skills.Add(skill);
        return skill;
    }

    private PracticeDay Day(DateOnly date)
    {
        var day = _days.FirstOrDefault(d => d.Date == date);
        if (day is not null) return day;
        day = new PracticeDay(date);
        _days.Add(day);
        return day;
    }
}
