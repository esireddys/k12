using K12.Application.Abstractions;
using K12.Application.Practice.Sets;
using K12.Domain.Accounts;
using K12.Domain.Common;
using K12.Domain.Content;
using K12.Domain.Curriculum;
using K12.Domain.Progress;

namespace K12.Application.Progress;

/// <summary>
/// A kid's answers: scored like a guest's, and also saved to the kid's history and progress.
/// Practice sends one answer at a time; an exam sends all of them at Finish with <paramref name="Finish"/> set.
/// </summary>
/// <param name="SessionId">Chosen by the device when the set starts; every batch of the set uses it.</param>
/// <param name="Source">Where the set came from, for the parent's history ("Topic", "Year", "Shuffle", "Mistakes").</param>
/// <param name="Title">The set's title as the screen showed it.</param>
/// <param name="LocalDate">The kid's date on their device, for streaks.</param>
/// <param name="ElapsedSeconds">Time spent on these answers, as the device measured it.</param>
public sealed record RecordAnswersCommand(
    Guid AccountId, Guid KidId, Guid SessionId, PracticeMode Mode, string Source, string Title,
    DateOnly? LocalDate, int ElapsedSeconds, IReadOnlyList<ResponseItem> Responses, bool Finish);

/// <summary>Ends a practice set, for example when the kid taps "See results".</summary>
public sealed record FinishSessionCommand(Guid AccountId, Guid KidId, Guid SessionId);

public sealed record SessionView(
    Guid Id, PracticeMode Mode, string Source, string Title, int Correct, int Total, int Minutes,
    DateTimeOffset StartedAt, DateTimeOffset? FinishedAt);

/// <param name="Session">The set so far, after these answers.</param>
public sealed record RecordedScore(ScoreResult Score, SessionView Session);

public sealed class RecordAnswersHandler(
    IParentAccountRepository accounts, IQuestionRepository questions, ISkillRepository skills,
    IPracticeSessionRepository sessions, IKidProgressRepository progress, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<RecordAnswersCommand, RecordedScore>
{
    public async Task<Result<RecordedScore>> HandleAsync(RecordAnswersCommand command, CancellationToken ct = default)
    {
        var family = await KidAccess.LoadAsync(accounts, command.AccountId, command.KidId, ct);
        if (!family.IsSuccess) return family.Error!;
        var kid = family.Value!.Kid;
        if (kid.CourseId is not { } courseId) return KidAccess.NoCourse;
        if (command.SessionId == Guid.Empty) return Error.Validation("progress.no_session", "A set needs an id.");

        var scored = await Scoring.ScoreAsync(questions, command.Responses, ct);
        if (!scored.IsSuccess) return scored.Error!;

        var now = clock.UtcNow;
        var sessionId = new PracticeSessionId(command.SessionId);
        var session = await sessions.GetAsync(sessionId, ct);
        if (session is null)
        {
            session = PracticeSession.Start(sessionId, kid.Id, courseId, command.Mode, command.Source, command.Title, now);
            await sessions.AddAsync(session, ct);
        }
        else if (session.KidId != kid.Id)
        {
            return Error.Conflict("progress.session_taken", "That set belongs to someone else. Start a new one.");
        }

        var courseSkills = (await skills.ListByCourseAsync(courseId, ct)).Select(s => s.Id).ToHashSet();
        var kidProgress = await progress.GetAsync(kid.Id, ct);
        if (kidProgress is null)
        {
            kidProgress = KidProgress.Start(kid.Id);
            await progress.AddAsync(kidProgress, ct);
        }

        try
        {
            var facts = new List<AnswerFact>();
            foreach (var item in scored.Value!.Result.Items)
            {
                var question = scored.Value.Questions[item.QuestionId];
                var skill = KidAccess.SkillIn(question.SkillIds, courseSkills);
                var response = command.Responses.First(r => r.QuestionId == item.QuestionId).Response;
                // Only the first answer to a question in a set counts.
                if (session.Answer(question.Id, skill, response, item.IsCorrect, now))
                    facts.Add(new AnswerFact(question.Id, skill, item.Answered, item.IsCorrect));
            }

            session.AddTime(command.ElapsedSeconds, facts.Count);
            kidProgress.Record(facts, KidAccess.Today(command.LocalDate, clock), command.ElapsedSeconds, now);
            if (command.Finish) session.Finish(now);
        }
        catch (DomainException e)
        {
            return Error.Validation("progress.invalid", e.Message);
        }

        await unitOfWork.SaveChangesAsync(ct);
        return new RecordedScore(scored.Value.Result, Sessions.View(session));
    }
}

public sealed class FinishSessionHandler(
    IParentAccountRepository accounts, IPracticeSessionRepository sessions, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<FinishSessionCommand, SessionView>
{
    public async Task<Result<SessionView>> HandleAsync(FinishSessionCommand command, CancellationToken ct = default)
    {
        var family = await KidAccess.LoadAsync(accounts, command.AccountId, command.KidId, ct);
        if (!family.IsSuccess) return family.Error!;
        var session = await sessions.GetAsync(new PracticeSessionId(command.SessionId), ct);
        if (session is null || session.KidId != family.Value!.Kid.Id)
            return Error.NotFound("progress.no_session", "No such set.");
        session.Finish(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(ct);
        return Sessions.View(session);
    }
}

internal static class Sessions
{
    public static SessionView View(PracticeSession s) =>
        new(s.Id.Value, s.Mode, s.Source, s.Title, s.Correct, s.Total, s.Minutes, s.StartedAt, s.FinishedAt);
}
