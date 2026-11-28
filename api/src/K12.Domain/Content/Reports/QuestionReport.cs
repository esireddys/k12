using K12.Domain.Common;

namespace K12.Domain.Content.Reports;

public readonly record struct QuestionReportId(Guid Value)
{
    public static QuestionReportId New() => new(Guid.NewGuid());
}

/// <summary>
/// What a family says is wrong with a question. A fixed list on purpose: kids report too, and free text from a child
/// could hold their name, school or anything else we promise not to keep.
/// </summary>
public enum ReportReason
{
    /// <summary>The key looks wrong.</summary>
    WrongAnswer,
    /// <summary>The picture or text is too small, cut off or blurry.</summary>
    HardToRead,
    /// <summary>The question does not make sense, or something is missing from it.</summary>
    Confusing,
    /// <summary>A typo or wrong word.</summary>
    Typo,
    /// <summary>Anything else.</summary>
    Other,
}

/// <summary>Who sent a report. Never which kid or parent: the owners only need to know where it came from.</summary>
public enum Reporter { Guest, Kid, Parent }

public sealed record QuestionReported(QuestionReportId ReportId, QuestionId QuestionId, DateTimeOffset OccurredAt) : IDomainEvent;

/// <summary>
/// One problem a guest, kid or parent reported on a Live question, waiting for an owner. The owner fixes the
/// question (or decides it is fine) and closes the report. It holds no personal data.
/// </summary>
public sealed class QuestionReport : AggregateRoot<QuestionReportId>
{
    /// <summary>For the persistence layer only.</summary>
    private QuestionReport() : base(default) { }

    private QuestionReport(QuestionReportId id, QuestionId questionId, int questionVersion, ReportReason reason, Reporter reporter, DateTimeOffset at)
        : base(id)
    {
        QuestionId = questionId;
        QuestionVersion = questionVersion;
        Reason = reason;
        Reporter = reporter;
        ReportedAt = at;
    }

    public QuestionId QuestionId { get; }

    /// <summary>The version the reporter saw, so an owner knows whether a later fix already covers it.</summary>
    public int QuestionVersion { get; }
    public ReportReason Reason { get; }
    public Reporter Reporter { get; }
    public DateTimeOffset ReportedAt { get; }

    /// <summary>The owner who closed it, once closed.</summary>
    public string? ClosedBy { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }
    public bool IsOpen => ClosedAt is null;

    /// <summary>Reports a problem with a question students can see. Only Live questions are reported.</summary>
    public static QuestionReport File(Question question, ReportReason reason, Reporter reporter, DateTimeOffset at)
    {
        if (!question.IsVisibleToStudents) throw new DomainException("Only a question students can see can be reported.");
        if (!Enum.IsDefined(reason)) throw new DomainException("Pick what is wrong with the question.");
        if (!Enum.IsDefined(reporter)) throw new DomainException("Unknown reporter.");
        var report = new QuestionReport(QuestionReportId.New(), question.Id, question.Version, reason, reporter, at);
        report.Raise(new QuestionReported(report.Id, question.Id, at));
        return report;
    }

    /// <summary>An owner has dealt with the report: fixed the question, or checked it is right.</summary>
    public void Close(string owner, DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(owner)) throw new DomainException("Closing a report must name the owner.");
        if (!IsOpen) throw new DomainException("This report is already closed.");
        ClosedBy = owner.Trim();
        ClosedAt = at;
    }
}
