using System.Text.Json;
using K12.Domain.Content;
using K12.Domain.Content.Answers;
using K12.Domain.Curriculum;

namespace K12.Application.Practice.Sets;

public sealed record ChoiceView(string Label, string Text);

/// <summary>Where a released question came from, for the line under it ("2022 · item 7"). Names are data.</summary>
public sealed record QuestionSourceView(string Exam, int Year, string Season, int ItemNumber);

/// <summary>
/// One question as a student sees it: never the answer or the explanation, which come back only after
/// the student answers (practice) or finishes (exam). The answer kind is shown so the device can offer
/// the right number pad keys.
/// </summary>
/// <param name="TextNeedsReview">The text was read from a PDF and may be rough: the picture is what to read.</param>
/// <param name="Category">The reporting category the question counts toward: its own, or else its skill's.</param>
/// <param name="PassageId">The reading selection to show beside the question, one of the set's passages.</param>
public sealed record StudentQuestion(
    Guid Id, string ItemType, AnswerKind AnswerKind, string Stem, IReadOnlyList<ChoiceView> Choices,
    bool HasImage, bool ShowImage, bool TextNeedsReview, string? FigureDescription,
    string? SkillCode, string? SkillDescription, int? Category, Difficulty Difficulty, QuestionSourceView? Source,
    Guid? PassageId = null);

/// <summary>The parts of a question's content JSON the student screens use. Anything else is ignored.</summary>
internal sealed record QuestionContent(
    string? Image, string Stem, IReadOnlyList<ChoiceView> Choices, bool ShowImage, bool TextNeedsReview,
    string? FigureDescription, string? Explanation)
{
    public static QuestionContent Read(string json)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        var root = doc.RootElement;
        string? Str(string name) =>
            root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString())
                ? v.GetString()!.Trim() : null;
        bool Bool(string name, bool fallback) =>
            root.TryGetProperty(name, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False ? v.GetBoolean() : fallback;

        var choices = new List<ChoiceView>();
        if (root.TryGetProperty("choices", out var list) && list.ValueKind == JsonValueKind.Array)
        {
            foreach (var c in list.EnumerateArray())
            {
                if (c.ValueKind != JsonValueKind.Object) continue;
                var label = c.TryGetProperty("label", out var l) ? l.GetString() ?? "" : "";
                var text = c.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "";
                if (label.Length > 0) choices.Add(new ChoiceView(label.Trim(), text.Trim()));
            }
        }

        return new QuestionContent(
            Str("image"), Str("stem") ?? "", choices, Bool("showImage", true), Bool("textNeedsReview", false),
            Str("figureDescription"), Str("explanation"));
    }
}

/// <summary>Builds student views for a batch of questions from data already loaded.</summary>
internal static class StudentQuestions
{
    public static StudentQuestion View(
        Question q, IReadOnlyDictionary<SkillId, Skill> skills, IReadOnlyDictionary<QuestionId, QuestionSourceView> sources)
    {
        var content = QuestionContent.Read(q.ContentJson);
        var skill = q.SkillIds.Where(skills.ContainsKey).Select(id => skills[id]).FirstOrDefault();
        return new StudentQuestion(
            q.Id.Value, q.ItemType, q.Answer.Kind, content.Stem, content.Choices,
            content.Image is not null, content.ShowImage || content.TextNeedsReview, content.TextNeedsReview,
            content.FigureDescription, skill?.Code, skill?.Description, CategoryOf(q, skills), q.Difficulty,
            sources.GetValueOrDefault(q.Id), q.PassageId?.Value);
    }

    /// <summary>The reporting category a question counts toward: the item's own when the key gave one, else its first skill's.</summary>
    public static int? CategoryOf(Question q, IReadOnlyDictionary<SkillId, Skill> skills) =>
        q.ReportingCategory ?? q.SkillIds.Where(skills.ContainsKey).Select(id => skills[id].CategoryNumber).FirstOrDefault(n => n is not null);

    public static Dictionary<QuestionId, QuestionSourceView> Sources(IEnumerable<ExamForm> forms)
    {
        var map = new Dictionary<QuestionId, QuestionSourceView>();
        foreach (var form in forms)
        foreach (var item in form.Items)
            map.TryAdd(item.QuestionId, new QuestionSourceView(form.Exam, form.Year, form.Season, item.Number));
        return map;
    }
}
