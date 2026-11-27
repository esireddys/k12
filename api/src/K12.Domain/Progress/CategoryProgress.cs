namespace K12.Domain.Progress;

/// <summary>One checked answer, placed in the reporting category its question counts toward.</summary>
public sealed record CategoryAnswer(int Category, bool IsCorrect, DateTimeOffset At);

/// <summary>How a kid is doing in one reporting category of their course.</summary>
/// <param name="Recent">The latest answers in the category, oldest first ('1' right, '0' wrong).</param>
public sealed record CategoryStanding(int Category, int Answered, int Correct, string Recent)
{
    public MasteryLevel Level => Mastery.Level(Recent, Mastery.Category);

    /// <summary>Share of all answers that were right, 0 to 100; empty before any answer.</summary>
    public int? Accuracy => Answered == 0 ? null : (int)Math.Round(100.0 * Correct / Answered);
}

/// <summary>
/// Progress by reporting category, the way the state test reports it. It is worked out from the kid's answer
/// history (<see cref="PracticeSession"/>), so it covers every answer ever given, including those before it existed.
/// A question counts toward its own category when the released test gave one, else toward its skill's.
/// </summary>
public static class CategoryProgress
{
    /// <param name="categories">The course's category numbers. Each gets a standing, answered or not.</param>
    public static IReadOnlyList<CategoryStanding> From(IEnumerable<int> categories, IEnumerable<CategoryAnswer> answers)
    {
        var byCategory = answers.GroupBy(a => a.Category).ToDictionary(g => g.Key, g => g.OrderBy(a => a.At).ToList());
        return categories.Distinct().Order().Select(number =>
        {
            var list = byCategory.GetValueOrDefault(number) ?? [];
            var recent = list.Aggregate(string.Empty, (r, a) => Mastery.Push(r, a.IsCorrect, Mastery.Category));
            return new CategoryStanding(number, list.Count, list.Count(a => a.IsCorrect), recent);
        }).ToList();
    }
}
