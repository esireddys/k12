using K12.Domain.Accounts;
using K12.Domain.Common;
using K12.Domain.Content;
using K12.Domain.Curriculum;
using K12.Domain.Progress;

namespace K12.Domain.Tests.Progress;

public class KidProgressTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 27, 15, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 11, 27);
    private static readonly SkillId Skill = SkillId.New();

    private static AnswerFact Right(QuestionId q) => new(q, Skill, true, true);
    private static AnswerFact Wrong(QuestionId q) => new(q, Skill, true, false);

    [Fact]
    public void Answers_count_toward_the_skill_and_the_day()
    {
        var p = KidProgress.Start(KidProfileId.New());
        p.Record([Right(QuestionId.New()), Wrong(QuestionId.New()), new AnswerFact(QuestionId.New(), null, false, false)], Today, 90, At);

        var skill = Assert.Single(p.Skills);
        Assert.Equal((Skill, 2, 1, "10"), (skill.SkillId, skill.Answered, skill.Correct, skill.Recent));
        var day = Assert.Single(p.Days);
        Assert.Equal((Today, 3, 1, 90), (day.Date, day.Answered, day.Correct, day.Seconds));
        Assert.Equal((3, 1), (p.TotalAnswered, p.TotalCorrect));
    }

    [Fact]
    public void A_missed_question_stays_on_the_list_until_it_is_answered_right()
    {
        var p = KidProgress.Start(KidProfileId.New());
        var q = QuestionId.New();

        p.Record([Wrong(q)], Today, 10, At);
        p.Record([Wrong(q)], Today, 10, At.AddMinutes(5));
        var mistake = Assert.Single(p.OpenMistakes);
        Assert.Equal((2, At, At.AddMinutes(5)), (mistake.TimesMissed, mistake.FirstMissedAt, mistake.LastMissedAt));

        p.Record([Right(q)], Today, 10, At.AddMinutes(10));
        Assert.Empty(p.OpenMistakes);
        Assert.Equal(At.AddMinutes(10), Assert.Single(p.Mistakes).FixedAt);

        // Missing it again later puts it back.
        p.Record([Wrong(q)], Today, 10, At.AddMinutes(20));
        Assert.Equal(3, Assert.Single(p.OpenMistakes).TimesMissed);
    }

    [Fact]
    public void A_right_answer_never_adds_to_the_mistakes()
    {
        var p = KidProgress.Start(KidProfileId.New());
        p.Record([Right(QuestionId.New())], Today, 5, At);
        Assert.Empty(p.Mistakes);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public void The_device_date_may_be_a_day_off_for_time_zones(int days)
    {
        var p = KidProgress.Start(KidProfileId.New());
        p.Record([Right(QuestionId.New())], Today.AddDays(days), 5, At);
        Assert.Equal(Today.AddDays(days), Assert.Single(p.Days).Date);
    }

    [Theory]
    [InlineData(-2)]
    [InlineData(3)]
    public void A_device_date_far_off_is_refused(int days) =>
        Assert.Throws<DomainException>(() => KidProgress.Start(KidProfileId.New()).Record([Right(QuestionId.New())], Today.AddDays(days), 5, At));

    [Theory]
    [InlineData(-5, 1, 0)]
    [InlineData(300, 1, 300)]
    [InlineData(5000, 1, KidProgress.MaxSecondsPerAnswer)] // a tablet left open does not count
    [InlineData(5000, 2, 2 * KidProgress.MaxSecondsPerAnswer)]
    public void Time_is_capped_per_answer(int seconds, int answers, int expected)
    {
        var p = KidProgress.Start(KidProfileId.New());
        p.Record(Enumerable.Range(0, answers).Select(_ => Right(QuestionId.New())).ToList(), Today, seconds, At);
        Assert.Equal(expected, Assert.Single(p.Days).Seconds);
    }

    /// <param name="daysAgo">The days the kid practiced, counted back from today.</param>
    [Theory]
    [InlineData(new int[0], 0, 0)]
    [InlineData(new[] { 0 }, 1, 1)]
    [InlineData(new[] { 1 }, 1, 1)] // practiced yesterday, not yet today: the streak still stands
    [InlineData(new[] { 2 }, 0, 1)] // a whole day missed: broken
    [InlineData(new[] { 0, 1, 2, 3, 4 }, 5, 5)]
    [InlineData(new[] { 1, 2, 3 }, 3, 3)]
    [InlineData(new[] { 0, 1, 3, 4, 5, 6 }, 2, 4)]
    public void Streaks_count_days_in_a_row(int[] daysAgo, int current, int best)
    {
        // Record on each day's own date: the clock moves with the day so the skew check passes.
        var q = KidProgress.Start(KidProfileId.New());
        foreach (var ago in daysAgo.OrderDescending())
            q.Record([Right(QuestionId.New())], Today.AddDays(-ago), 5, At.AddDays(-ago));

        Assert.Equal((current, best), (q.CurrentStreak(Today), q.BestStreak()));
    }

    [Fact]
    public void Nothing_recorded_for_an_empty_batch()
    {
        var p = KidProgress.Start(KidProfileId.New());
        p.Record([], Today, 100, At);
        Assert.Empty(p.Days);
    }
}
