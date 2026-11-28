using System.Text.Json;
using K12.Domain.Content;
using K12.Domain.Content.Answers;
using K12.Domain.Content.Review;
using K12.Domain.Curriculum;

namespace K12.Application.ContentReview;

/// <summary>A skill a question is tagged to, with the rule sheet the reviewer checks it against.</summary>
public sealed record ReviewSkill(string Code, string Description, JsonElement Limits);

/// <summary>Where a question came from. Names are data from the import, never assumed.</summary>
public sealed record ReviewSource(
    string Jurisdiction, string Subject, string CourseCode, string CourseName, string FrameworkName,
    string Exam, int Year, string Season, int ItemNumber, string SourceUrl);

public sealed record AiReviewView(
    string Reviewer, DateTimeOffset ReviewedAt, AiVerdict Verdict, bool AnswerKeyAgrees, string SolvedAnswer,
    Difficulty SuggestedDifficulty, JsonElement ProposedContent, string Summary, IReadOnlyList<AiFinding> Findings);

public sealed record OwnerDecisionView(OwnerOutcome Outcome, string DecidedBy, DateTimeOffset DecidedAt, string? Note);

/// <summary>Everything a reviewer, AI or owner, needs to judge one question.</summary>
public sealed record QuestionForReview(
    Guid Id, int Version, QuestionStatus Status, QuestionOrigin Origin, string ItemType,
    JsonElement Content, AnswerSpec Answer, Difficulty Difficulty,
    IReadOnlyList<ReviewSkill> Skills, ReviewSource? Source,
    AiReviewView? AiReview, OwnerDecisionView? OwnerDecision);

/// <summary>
/// Builds review views for a batch of questions with one lookup per repository,
/// so the AI queue and the owner queue cost the same few queries however long they are.
/// </summary>
internal static class ReviewViews
{
    public static async Task<IReadOnlyList<QuestionForReview>> BuildAsync(
        IReadOnlyList<Question> questions, ISkillRepository skills, IExamFormRepository forms, ICourseRepository courses,
        CancellationToken ct)
    {
        if (questions.Count == 0) return [];

        var skillIds = questions.SelectMany(q => q.SkillIds).Distinct().ToList();
        var skillsById = (await skills.ListByIdsAsync(skillIds, ct)).ToDictionary(s => s.Id);

        var questionIds = questions.Select(q => q.Id).ToList();
        var formList = await forms.ListContainingAsync(questionIds, ct);
        var coursesById = (await courses.ListByIdsAsync(formList.Select(f => f.CourseId).Distinct().ToList(), ct))
            .ToDictionary(c => c.Id);

        var sourceByQuestion = new Dictionary<QuestionId, ReviewSource>();
        foreach (var form in formList)
        {
            if (!coursesById.TryGetValue(form.CourseId, out var course)) continue;
            foreach (var item in form.Items)
            {
                sourceByQuestion.TryAdd(item.QuestionId, new ReviewSource(
                    course.Jurisdiction, course.Subject, course.Code, course.Name, course.FrameworkName,
                    form.Exam, form.Year, form.Season, item.Number, form.SourceUrl));
            }
        }

        return questions.Select(q => new QuestionForReview(
            q.Id.Value, q.Version, q.Status, q.Origin, q.ItemType, Json(q.ContentJson), q.Answer, q.Difficulty,
            q.SkillIds.Where(skillsById.ContainsKey).Select(id => skillsById[id])
                .Select(s => new ReviewSkill(s.Code, s.Description, Json(s.LimitsJson))).ToList(),
            sourceByQuestion.GetValueOrDefault(q.Id),
            q.AiReview is { } r
                ? new AiReviewView(r.Reviewer, r.ReviewedAt, r.Verdict, r.AnswerKeyAgrees, r.SolvedAnswer,
                    r.SuggestedDifficulty, Json(r.ProposedContentJson), r.Summary, r.Findings)
                : null,
            q.OwnerDecision is { } d ? new OwnerDecisionView(d.Outcome, d.DecidedBy, d.DecidedAt, d.Note) : null))
            .ToList();
    }

    public static JsonElement Json(string json)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        return doc.RootElement.Clone();
    }
}
