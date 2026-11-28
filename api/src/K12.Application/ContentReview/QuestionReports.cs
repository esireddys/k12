using K12.Application.Abstractions;
using K12.Domain.Common;
using K12.Domain.Content;
using K12.Domain.Content.Reports;
using K12.Domain.Curriculum;

namespace K12.Application.ContentReview;

/// <summary>A guest, kid or parent says something is wrong with a question they were shown.</summary>
public sealed record ReportQuestionCommand(Guid QuestionId, ReportReason Reason, Reporter Reporter);

public sealed record QuestionReportFiled(Guid ReportId);

/// <summary>Files a report on a Live question. Holds only the reason and who sent it (guest, kid or parent), never who.</summary>
public sealed class ReportQuestionHandler(
    IQuestionRepository questions, IQuestionReportRepository reports, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<ReportQuestionCommand, QuestionReportFiled>
{
    public async Task<Result<QuestionReportFiled>> HandleAsync(ReportQuestionCommand command, CancellationToken ct = default)
    {
        var question = await questions.GetAsync(new QuestionId(command.QuestionId), ct);
        if (question is null || !question.IsVisibleToStudents) return Error.NotFound("question.not_found", "No such question.");
        try
        {
            var report = QuestionReport.File(question, command.Reason, command.Reporter, clock.UtcNow);
            await reports.AddAsync(report, ct);
            await unitOfWork.SaveChangesAsync(ct);
            return new QuestionReportFiled(report.Id.Value);
        }
        catch (DomainException e)
        {
            return Error.Validation("report.invalid", e.Message);
        }
    }
}

public sealed record ListQuestionReportsQuery(int Take = 200);

/// <summary>One open report, with where its question came from so an owner can find it.</summary>
/// <param name="Fixed">The question has changed since it was reported, so the owner may only need to check and close it.</param>
public sealed record QuestionReportView(
    Guid Id, Guid QuestionId, ReportReason Reason, Reporter Reporter, DateTimeOffset ReportedAt,
    ReviewSource? Source, IReadOnlyList<string> SkillCodes, bool Fixed);

public sealed record QuestionReportsResponse(IReadOnlyList<QuestionReportView> Reports);

/// <summary>Open reports from families, oldest first. Owners only.</summary>
public sealed class ListQuestionReportsHandler(
    IQuestionReportRepository reports, IQuestionRepository questions, ISkillRepository skills, IExamFormRepository forms,
    ICourseRepository courses)
    : IQueryHandler<ListQuestionReportsQuery, QuestionReportsResponse>
{
    public async Task<Result<QuestionReportsResponse>> HandleAsync(ListQuestionReportsQuery query, CancellationToken ct = default)
    {
        if (query.Take is < 1 or > 1000) return Error.Validation("report.take", "Take between 1 and 1000 reports.");
        var open = await reports.ListOpenAsync(query.Take, ct);
        var asked = await questions.ListByIdsAsync(open.Select(r => r.QuestionId).Distinct().ToList(), ct);
        var views = (await ReviewViews.BuildAsync(asked, skills, forms, courses, ct)).ToDictionary(v => v.Id);
        return new QuestionReportsResponse(open
            .Where(r => views.ContainsKey(r.QuestionId.Value))
            .Select(r =>
            {
                var q = views[r.QuestionId.Value];
                return new QuestionReportView(
                    r.Id.Value, r.QuestionId.Value, r.Reason, r.Reporter, r.ReportedAt, q.Source,
                    q.Skills.Select(s => s.Code).ToList(), q.Version != r.QuestionVersion);
            })
            .ToList());
    }
}

/// <summary>Closes every open report on a question, once an owner has fixed it or checked it is right.</summary>
public sealed record CloseQuestionReportsCommand(Guid QuestionId, string Owner);

public sealed record ReportsClosed(int Closed);

public sealed class CloseQuestionReportsHandler(IQuestionReportRepository reports, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<CloseQuestionReportsCommand, ReportsClosed>
{
    public async Task<Result<ReportsClosed>> HandleAsync(CloseQuestionReportsCommand command, CancellationToken ct = default)
    {
        var open = await reports.ListOpenForAsync(new QuestionId(command.QuestionId), ct);
        try
        {
            foreach (var report in open) report.Close(command.Owner, clock.UtcNow);
        }
        catch (DomainException e)
        {
            return Error.Validation("report.invalid", e.Message);
        }
        await unitOfWork.SaveChangesAsync(ct);
        return new ReportsClosed(open.Count);
    }
}
