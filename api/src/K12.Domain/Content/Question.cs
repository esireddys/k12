using K12.Domain.Common;
using System.Text.Json;
using K12.Domain.Content.Answers;
using K12.Domain.Content.Review;
using K12.Domain.Curriculum;

namespace K12.Domain.Content;

public readonly record struct QuestionId(Guid Value)
{
    public static QuestionId New() => new(Guid.NewGuid());
}

public sealed record QuestionApproved(QuestionId QuestionId, string ApprovedBy, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record QuestionRejected(QuestionId QuestionId, string RejectedBy, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record ReleasedQuestionPublished(QuestionId QuestionId, DateTimeOffset OccurredAt) : IDomainEvent;

/// <summary>
/// One question in the bank. Only Live questions are shown to students. How a question gets there depends on its origin:
/// <list type="bullet">
/// <item>Official questions come from released tests, which the publisher already reviewed. They go Live directly.</item>
/// <item>Generated questions (written by an AI agent) go through AI review, then owner approval, then Live.</item>
/// </list>
/// </summary>
public sealed class Question : AggregateRoot<QuestionId>
{
    private readonly List<SkillId> _skillIds = [];

    /// <summary>For the persistence layer only.</summary>
    private Question() : base(default)
    {
        ItemType = string.Empty;
        ContentJson = string.Empty;
        Answer = null!;
    }

    private Question(
        QuestionId id, IEnumerable<SkillId> skillIds, string itemType, string contentJson,
        AnswerSpec answer, Difficulty difficulty, QuestionOrigin origin, QuestionId? parentId,
        PassageId? passageId, int? reportingCategory) : base(id)
    {
        _skillIds = skillIds.ToList();
        if (_skillIds.Count == 0) throw new DomainException("A question must be tagged to at least one skill.");
        if (reportingCategory is < 1) throw new DomainException("Reporting category numbers start at 1.");
        PassageId = passageId;
        ReportingCategory = reportingCategory;
        ItemType = itemType;
        ContentJson = contentJson;
        Answer = answer;
        Difficulty = difficulty;
        Origin = origin;
        ParentId = parentId;
        Status = QuestionStatus.Draft;
        Version = 1;
    }

    public IReadOnlyList<SkillId> SkillIds => _skillIds;
    public string ItemType { get; }
    public string ContentJson { get; private set; }
    public AnswerSpec Answer { get; private set; }
    public Difficulty Difficulty { get; private set; }
    public QuestionOrigin Origin { get; }
    public QuestionId? ParentId { get; }

    /// <summary>The reading selection this question is about, if any. Students see the passage beside it.</summary>
    public PassageId? PassageId { get; }

    /// <summary>
    /// The reporting category the test counted this item under, when the item says so. Some skills are reported
    /// in more than one category (a reading skill counts as literary or informational depending on the passage),
    /// so the item's own category wins over its skill's. Empty means "use the skill's category".
    /// </summary>
    public int? ReportingCategory { get; }
    public QuestionStatus Status { get; private set; }
    public int Version { get; private set; }

    /// <summary>The AI reviewer's findings on the current version, once it has run.</summary>
    public AiReview? AiReview { get; private set; }

    /// <summary>The owner's decision on the current version, once one is made.</summary>
    public OwnerDecision? OwnerDecision { get; private set; }

    public static Question Draft(
        IEnumerable<SkillId> skillIds, string itemType, string contentJson, AnswerSpec answer,
        Difficulty difficulty, QuestionOrigin origin, QuestionId? parentId = null,
        PassageId? passageId = null, int? reportingCategory = null) =>
        new(QuestionId.New(), skillIds, itemType, contentJson, answer, difficulty, origin, parentId, passageId, reportingCategory);

    /// <summary>Hands a generated question to the AI reviewer. Released questions never need review.</summary>
    public void SubmitForAiReview()
    {
        if (Origin is QuestionOrigin.Official)
            throw new DomainException("Questions from released tests go Live directly; only generated questions are reviewed.");
        Move(QuestionStatus.Draft, QuestionStatus.InAiReview);
    }

    /// <summary>States a released question may be published from: a new draft, or one left in review by an older import.</summary>
    public static readonly IReadOnlyList<QuestionStatus> PublishableReleasedStatuses =
        [QuestionStatus.Draft, QuestionStatus.InAiReview, QuestionStatus.AwaitingOwner];

    /// <summary>
    /// Puts a question from a released test in front of students. The publisher reviewed it, so no AI or owner
    /// review is needed. A question an owner rejected stays rejected, and a retired one stays retired.
    /// </summary>
    public void PublishReleased(DateTimeOffset at)
    {
        if (Origin is not QuestionOrigin.Official)
            throw new DomainException("Only questions from released tests can skip review; generated questions need an owner's approval.");
        if (!PublishableReleasedStatuses.Contains(Status))
            throw new DomainException($"Cannot publish a {Status} question.");
        Status = QuestionStatus.Live;
        Raise(new ReleasedQuestionPublished(Id, at));
    }

    /// <summary>
    /// Records the AI reviewer's findings and hands the question to the owners. The reviewer cannot reject:
    /// a question it is unsure about still goes to an owner, marked as needing attention.
    /// </summary>
    public void RecordAiReview(AiReview review)
    {
        review.EnsureComplete();
        EnsureJsonObject(review.ProposedContentJson, "The proposed content");
        Move(QuestionStatus.InAiReview, QuestionStatus.AwaitingOwner);
        AiReview = review;
    }

    /// <summary>An owner approves the question as it stands. It goes Live.</summary>
    public void Approve(string approvedBy, DateTimeOffset at, string? note = null) =>
        Decide(approvedBy, at, note, edits: null);

    /// <summary>
    /// An owner approves with their own edits (usually the AI reviewer's cleaned-up text, adjusted).
    /// The owner is the final reviewer, so the edited question goes Live as a new version.
    /// </summary>
    public void Approve(OwnerEdits edits, string approvedBy, DateTimeOffset at, string? note = null) =>
        Decide(approvedBy, at, note, edits);

    /// <summary>An owner keeps the question away from students. A reason is required so the next reader knows why.</summary>
    public void Reject(string rejectedBy, DateTimeOffset at, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new DomainException("Say why the question is rejected.");
        RequireOwner(rejectedBy);
        Move(QuestionStatus.AwaitingOwner, QuestionStatus.Rejected);
        OwnerDecision = new OwnerDecision(OwnerOutcome.Rejected, rejectedBy.Trim(), at, reason.Trim());
        Raise(new QuestionRejected(Id, rejectedBy.Trim(), at));
    }

    /// <summary>
    /// Editing a question makes a new version. A generated question goes back through review;
    /// a released one keeps its status (a Live correction of how it was transcribed stays Live).
    /// </summary>
    public void Revise(string contentJson, AnswerSpec answer)
    {
        if (Status is QuestionStatus.Retired) throw new DomainException("A retired question cannot be revised.");
        EnsureJsonObject(contentJson, "The content");
        ContentJson = contentJson;
        Answer = answer;
        Version++;
        if (Origin is QuestionOrigin.Generated) Status = QuestionStatus.Draft;
        AiReview = null;
        OwnerDecision = null;
    }

    /// <summary>
    /// Takes a newer import's reading of the same printed item: its pictures, the text read from the PDF and which
    /// of that text students see. Only a released question nobody has edited is refreshed (version 1), so an owner's
    /// correction is never overwritten. The key must be the same: a different key is a different question.
    /// Status and version stay as they are, because the item itself has not changed.
    /// </summary>
    /// <returns>True when the content changed.</returns>
    public bool RefreshReleasedContent(string contentJson, AnswerSpec answer)
    {
        if (Origin is not QuestionOrigin.Official)
            throw new DomainException("Only questions from released tests are refreshed from an import.");
        if (!SameAnswer(answer, Answer))
            throw new DomainException($"The import gives the key {answer.Value}, but the question's key is {Answer.Value}.");
        EnsureJsonObject(contentJson, "The content");
        if (Version != 1 || contentJson == ContentJson) return false;
        ContentJson = contentJson;
        return true;
    }

    public void Retire() => Status = QuestionStatus.Retired;

    public bool IsVisibleToStudents => Status == QuestionStatus.Live;

    public CheckResult Check(string? response) => AnswerChecker.Check(Answer, response);

    private void Decide(string approvedBy, DateTimeOffset at, string? note, OwnerEdits? edits)
    {
        RequireOwner(approvedBy);
        if (edits is not null) EnsureJsonObject(edits.ContentJson, "The content");
        Move(QuestionStatus.AwaitingOwner, QuestionStatus.Live);

        var changed = false;
        if (edits is not null)
        {
            changed = edits.ContentJson != ContentJson || !SameAnswer(edits.Answer, Answer) || edits.Difficulty != Difficulty;
            ContentJson = edits.ContentJson;
            Answer = edits.Answer;
            Difficulty = edits.Difficulty;
            if (changed) Version++;
        }

        var outcome = changed ? OwnerOutcome.ApprovedWithEdits : OwnerOutcome.Approved;
        OwnerDecision = new OwnerDecision(outcome, approvedBy.Trim(), at, string.IsNullOrWhiteSpace(note) ? null : note.Trim());
        Raise(new QuestionApproved(Id, approvedBy.Trim(), at));
    }

    private static bool SameAnswer(AnswerSpec a, AnswerSpec b) =>
        a.Kind == b.Kind && a.Value == b.Value && a.AcceptEquivalent == b.AcceptEquivalent
        && a.RequireSimplest == b.RequireSimplest && a.Accept.SequenceEqual(b.Accept);

    private static void RequireOwner(string who)
    {
        if (string.IsNullOrWhiteSpace(who)) throw new DomainException("A decision must name the owner who made it.");
    }

    private static void EnsureJsonObject(string json, string what)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) throw new DomainException($"{what} must be a JSON object.");
        }
        catch (JsonException)
        {
            throw new DomainException($"{what} is not valid JSON.");
        }
    }

    private void Move(QuestionStatus from, QuestionStatus to)
    {
        if (Status != from) throw new DomainException($"Cannot move a question from {Status} to {to}; it must be {from}.");
        Status = to;
    }
}
