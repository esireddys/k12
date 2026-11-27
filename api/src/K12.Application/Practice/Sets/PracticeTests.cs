using K12.Domain.Content;
using K12.Domain.Curriculum;

namespace K12.Application.Practice.Sets;

/// <summary>
/// Builds a full-length practice test from a course's blueprint. Questions that share a passage stay together and
/// come in whole: a reading test is a few selections with all their questions, like the real one.
/// </summary>
internal static class PracticeTests
{
    /// <summary>The set's header. Generic on purpose: the exam's own name is curriculum data shown elsewhere.</summary>
    public const string Title = "Full practice test";

    /// <summary>How many arrangements to try; the closest to the blueprint wins.</summary>
    private const int Tries = 200;

    /// <summary>
    /// Picks whole groups (a passage with all its questions, or one question) that come as close as possible to the
    /// blueprint: the right number of items first, then the right number in each category. Tries many random
    /// arrangements of <paramref name="shuffled"/> and keeps the best. Never more items than the blueprint says.
    /// </summary>
    public static List<Question> Build(
        PracticeTestBlueprint blueprint, IReadOnlyList<Question> shuffled, Func<Question, int?> categoryOf, Random random)
    {
        var groups = Groups(shuffled);
        var share = blueprint.Mix.ToDictionary(m => m.Category, m => m.Items);
        List<List<Question>>? best = null;
        var bestScore = int.MaxValue;

        for (var attempt = 0; attempt < Tries && bestScore > 0; attempt++)
        {
            var order = attempt == 0 ? groups : groups.OrderBy(_ => random.Next()).ToList();
            var chosen = Fill(blueprint.ItemCount, order, share, categoryOf);
            var score = Score(blueprint.ItemCount, chosen, share, categoryOf);
            if (score < bestScore) (best, bestScore) = (chosen, score);
        }

        // Passages first in the order picked; single questions mixed between them would split the reading.
        return (best ?? []).OrderBy(g => g[0].PassageId is null ? 1 : 0).SelectMany(g => g).ToList();
    }

    /// <summary>First groups that keep every category within its share, then any group that still fits.</summary>
    private static List<List<Question>> Fill(
        int itemCount, List<List<Question>> order, Dictionary<int, int> share, Func<Question, int?> categoryOf)
    {
        var counts = new Dictionary<int, int>();
        var chosen = new List<List<Question>>();
        var total = 0;
        foreach (var strict in new[] { true, false })
        foreach (var group in order)
        {
            if (total >= itemCount) break;
            if (chosen.Contains(group) || total + group.Count > itemCount) continue;
            var adds = group.GroupBy(q => categoryOf(q) ?? 0).ToDictionary(g => g.Key, g => g.Count());
            if (strict && adds.Any(a => share.TryGetValue(a.Key, out var max) && counts.GetValueOrDefault(a.Key) + a.Value > max)) continue;
            chosen.Add(group);
            total += group.Count;
            foreach (var (category, n) in adds) counts[category] = counts.GetValueOrDefault(category) + n;
        }
        return chosen;
    }

    /// <summary>0 is a perfect match. A missing item costs more than an item in the wrong category.</summary>
    private static int Score(int itemCount, List<List<Question>> chosen, Dictionary<int, int> share, Func<Question, int?> categoryOf)
    {
        var items = chosen.SelectMany(g => g).ToList();
        var counts = items.GroupBy(q => categoryOf(q) ?? 0).ToDictionary(g => g.Key, g => g.Count());
        return 3 * (itemCount - items.Count) + share.Sum(s => Math.Abs(counts.GetValueOrDefault(s.Key) - s.Value));
    }

    /// <summary>Whole groups (a passage's questions, or one question) until about <paramref name="count"/> questions.</summary>
    public static List<Question> TakeGroups(IReadOnlyList<Question> shuffled, int count)
    {
        var picked = new List<Question>();
        foreach (var group in Groups(shuffled))
        {
            if (picked.Count >= count) break;
            // Always take at least one group, even a passage with more questions than asked for.
            if (picked.Count > 0 && picked.Count + group.Count > count && group.Count > 1) continue;
            picked.AddRange(group);
        }
        return picked;
    }

    /// <summary>Each passage's questions as one group (in their first-seen order), each other question alone.</summary>
    private static List<List<Question>> Groups(IReadOnlyList<Question> shuffled) =>
        SetPassages.GroupByPassage(shuffled)
            .Aggregate(new List<List<Question>>(), (acc, q) =>
            {
                if (q.PassageId is { } id && acc.Count > 0 && acc[^1][0].PassageId == id) acc[^1].Add(q);
                else acc.Add([q]);
                return acc;
            });
}
