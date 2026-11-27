using System.Text.Json;
using K12.Domain.Content;

namespace K12.Application.Practice.Sets;

/// <summary>
/// A reading selection as a student sees it: its title, how many printed pages it has (served as pictures, the
/// source of truth) and its text when that text has been checked. Never anything about the answers.
/// </summary>
/// <param name="Text">The selection's text, or empty while the text read from the PDF still needs checking.</param>
public sealed record PassageView(Guid Id, string Title, int PageCount, string? Text);

/// <summary>The parts of a passage's content JSON the student screens use.</summary>
internal sealed record PassageContent(IReadOnlyList<string> Pages, string? Text, bool TextNeedsReview)
{
    public static PassageContent Read(string json)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        var root = doc.RootElement;
        var pages = root.TryGetProperty("pages", out var list) && list.ValueKind == JsonValueKind.Array
            ? list.EnumerateArray().Where(p => p.ValueKind == JsonValueKind.String).Select(p => p.GetString()!).Where(p => p.Length > 0).ToList()
            : [];
        var text = root.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(t.GetString())
            ? t.GetString()!.Trim() : null;
        var needsReview = !root.TryGetProperty("textNeedsReview", out var r) || r.ValueKind != JsonValueKind.False;
        return new PassageContent(pages, text, needsReview);
    }

    public static PassageView View(Passage passage)
    {
        var content = Read(passage.ContentJson);
        return new PassageView(passage.Id.Value, passage.Title, content.Pages.Count, content.TextNeedsReview ? null : content.Text);
    }
}

/// <summary>Loads the passages a set of questions needs, in the order the questions first use them.</summary>
internal static class SetPassages
{
    public static async Task<IReadOnlyList<PassageView>> ForAsync(
        IReadOnlyList<Question> questions, IPassageRepository passages, CancellationToken ct)
    {
        var ids = questions.Where(q => q.PassageId is not null).Select(q => q.PassageId!.Value).Distinct().ToList();
        if (ids.Count == 0) return [];
        var byId = (await passages.ListByIdsAsync(ids, ct)).ToDictionary(p => p.Id);
        return ids.Where(byId.ContainsKey).Select(id => PassageContent.View(byId[id])).ToList();
    }

    /// <summary>
    /// Puts each passage's questions in the order they were printed, so "paragraph 3" comes before "paragraph 9".
    /// Passages, and questions with none, stay where they are.
    /// </summary>
    public static List<Question> InPrintedOrder(IReadOnlyList<Question> questions, IReadOnlyDictionary<QuestionId, QuestionSourceView> sources)
    {
        var grouped = GroupByPassage(questions);
        var result = new List<Question>(grouped.Count);
        for (var i = 0; i < grouped.Count;)
        {
            var end = i + 1;
            if (grouped[i].PassageId is { } id)
                while (end < grouped.Count && grouped[end].PassageId == id) end++;
            result.AddRange(grouped.Skip(i).Take(end - i)
                .Select((q, k) => (q, k))
                .OrderBy(x => sources.GetValueOrDefault(x.q.Id)?.ItemNumber ?? int.MaxValue).ThenBy(x => x.k)
                .Select(x => x.q));
            i = end;
        }
        return result;
    }

    /// <summary>
    /// Keeps each passage's questions together, so a student reads a selection once and answers all its questions
    /// in a row. Passages (and questions with none) keep the order they first appear in; questions keep theirs.
    /// </summary>
    public static List<Question> GroupByPassage(IEnumerable<Question> questions)
    {
        var groups = new List<(PassageId? Passage, List<Question> Items)>();
        foreach (var q in questions)
        {
            var group = q.PassageId is { } id ? groups.FindIndex(g => g.Passage == id) : -1;
            if (group >= 0) groups[group].Items.Add(q);
            else groups.Add((q.PassageId, [q]));
        }
        return groups.SelectMany(g => g.Items).ToList();
    }
}
