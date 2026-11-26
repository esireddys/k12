using K12.Domain.Common;
using K12.Domain.Content.Answers;
using K12.Domain.Curriculum;

namespace K12.Domain.Content;

public readonly record struct QuestionId(Guid Value)
{
    public static QuestionId New() => new(Guid.NewGuid());
}

public sealed record QuestionApproved(QuestionId QuestionId, string ApprovedBy, DateTimeOffset OccurredAt) : IDomainEvent;

/// <summary>
/// One question in the bank. Its lifecycle is Draft, AI review, owner approval, then Live.
/// Only Live questions are shown to students.
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
        AnswerSpec answer, Difficulty difficulty, QuestionOrigin origin, QuestionId? parentId) : base(id)
    {
        _skillIds = skillIds.ToList();
        if (_skillIds.Count == 0) throw new DomainException("A question must be tagged to at least one skill.");
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
    public QuestionStatus Status { get; private set; }
    public int Version { get; private set; }

    public static Question Draft(
        IEnumerable<SkillId> skillIds, string itemType, string contentJson, AnswerSpec answer,
        Difficulty difficulty, QuestionOrigin origin, QuestionId? parentId = null) =>
        new(QuestionId.New(), skillIds, itemType, contentJson, answer, difficulty, origin, parentId);

    public void SubmitForAiReview() => Move(QuestionStatus.Draft, QuestionStatus.InAiReview);

    public void PassAiReview() => Move(QuestionStatus.InAiReview, QuestionStatus.AwaitingOwner);

    public void Approve(string approvedBy, DateTimeOffset at)
    {
        Move(QuestionStatus.AwaitingOwner, QuestionStatus.Live);
        Raise(new QuestionApproved(Id, approvedBy, at));
    }

    public void Reject() => Move(QuestionStatus.AwaitingOwner, QuestionStatus.Rejected);

    /// <summary>Editing a reviewed question makes a new version that goes back through review.</summary>
    public void Revise(string contentJson, AnswerSpec answer)
    {
        if (Status is QuestionStatus.Retired) throw new DomainException("A retired question cannot be revised.");
        ContentJson = contentJson;
        Answer = answer;
        Version++;
        Status = QuestionStatus.Draft;
    }

    public void Retire() => Status = QuestionStatus.Retired;

    public bool IsVisibleToStudents => Status == QuestionStatus.Live;

    public CheckResult Check(string? response) => AnswerChecker.Check(Answer, response);

    private void Move(QuestionStatus from, QuestionStatus to)
    {
        if (Status != from) throw new DomainException($"Cannot move a question from {Status} to {to}; it must be {from}.");
        Status = to;
    }
}
