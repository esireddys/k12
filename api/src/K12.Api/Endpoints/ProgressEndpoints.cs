using System.Security.Claims;
using K12.Api.Auth;
using K12.Application.Abstractions;
using K12.Application.Accounts;
using K12.Application.Practice.Sets;
using K12.Application.Progress;
using K12.Domain.Accounts;
using K12.Domain.Progress;

namespace K12.Api.Endpoints;

/// <summary>
/// A signed-in family's kids: their answers are saved, their home screen shows streaks and mistakes to fix, and the
/// parent sees a dashboard behind the parent PIN. Guests never reach any of this, so nothing a guest does is saved.
/// </summary>
public static class ProgressEndpoints
{
    /// <param name="SessionId">Chosen by the device when the set starts; the same for every batch of the set.</param>
    /// <param name="LocalDate">The device's date (yyyy-MM-dd), so streaks follow the kid's own days.</param>
    /// <param name="ElapsedSeconds">Time spent on these answers, as the device measured it.</param>
    /// <param name="Finish">An exam sends every answer at once and finishes in the same call.</param>
    public sealed record RecordBody(
        Guid SessionId, PracticeMode Mode, string? Source, string? Title, DateOnly? LocalDate, int ElapsedSeconds,
        IReadOnlyList<ResponseItem> Responses, bool Finish);

    public sealed record PinBody(string Pin);

    /// <param name="ParentAreaOpenUntil">Set while the parent areas are open on this device.</param>
    public sealed record ParentGateResponse(bool HasPin, DateTimeOffset? LockedUntil, DateTimeOffset? ParentAreaOpenUntil);

    public sealed record UnlockResponse(PinCheck Result, int TriesLeft, DateTimeOffset? LockedUntil, DateTimeOffset? ParentAreaOpenUntil);

    public static IEndpointRouteBuilder MapProgressEndpoints(this IEndpointRouteBuilder app)
    {
        var kids = app.MapGroup("/api/family/kids/{kidId:guid}").WithTags("Progress").RequireAuthorization(Access.ParentPolicy);

        kids.MapPost("/answers", async (
                Guid kidId, RecordBody body, ClaimsPrincipal user,
                ICommandHandler<RecordAnswersCommand, RecordedScore> handler, CancellationToken ct) =>
            (await handler.HandleAsync(new RecordAnswersCommand(
                user.AccountId(), kidId, body.SessionId, body.Mode, body.Source ?? "", body.Title ?? "", body.LocalDate,
                body.ElapsedSeconds, body.Responses ?? [], body.Finish), ct)).ToHttp())
            .WithName("RecordAnswers")
            .Produces<RecordedScore>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        kids.MapPost("/sessions/{sessionId:guid}/finish", async (
                Guid kidId, Guid sessionId, ClaimsPrincipal user,
                ICommandHandler<FinishSessionCommand, SessionView> handler, CancellationToken ct) =>
            (await handler.HandleAsync(new FinishSessionCommand(user.AccountId(), kidId, sessionId), ct)).ToHttp())
            .WithName("FinishSession")
            .Produces<SessionView>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        kids.MapGet("/summary", async (
                Guid kidId, DateOnly? localDate, ClaimsPrincipal user,
                IQueryHandler<GetKidSummaryQuery, KidSummary> handler, CancellationToken ct) =>
            (await handler.HandleAsync(new GetKidSummaryQuery(user.AccountId(), kidId, localDate), ct)).ToHttp())
            .WithName("GetKidSummary")
            .Produces<KidSummary>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        kids.MapGet("/mistakes", async (
                Guid kidId, int? count, ClaimsPrincipal user,
                IQueryHandler<GetMistakesSetQuery, PracticeSet> handler, CancellationToken ct) =>
            (await handler.HandleAsync(new GetMistakesSetQuery(user.AccountId(), kidId, count), ct)).ToHttp())
            .WithName("GetMistakesSet")
            .Produces<PracticeSet>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        kids.MapGet("/today", async (
                Guid kidId, int? count, int? seed, ClaimsPrincipal user,
                IQueryHandler<GetTodaySetQuery, PracticeSet> handler, CancellationToken ct) =>
            (await handler.HandleAsync(new GetTodaySetQuery(user.AccountId(), kidId, count, seed), ct)).ToHttp())
            .WithName("GetTodaySet")
            .Produces<PracticeSet>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        kids.MapGet("/dashboard", async (
                Guid kidId, Guid? courseId, DateOnly? localDate, ClaimsPrincipal user,
                IQueryHandler<GetKidDashboardQuery, KidDashboard> handler, CancellationToken ct) =>
            (await handler.HandleAsync(new GetKidDashboardQuery(user.AccountId(), kidId, localDate, courseId), ct)).ToHttp())
            .WithName("GetKidDashboard")
            .RequireParentArea()
            .Produces<KidDashboard>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        var pin = app.MapGroup("/api/family/pin").WithTags("Parent PIN").RequireAuthorization(Access.ParentPolicy);

        pin.MapGet("", async (ClaimsPrincipal user, IClock clock, IQueryHandler<GetPinStatusQuery, PinStatus> handler, CancellationToken ct) =>
            {
                var result = await handler.HandleAsync(new GetPinStatusQuery(user.AccountId()), ct);
                if (!result.IsSuccess) return result.ToHttp();
                return Results.Ok(new ParentGateResponse(result.Value!.HasPin, result.Value.LockedUntil, user.ParentAreaOpenUntil(clock.UtcNow)));
            })
            .WithName("GetParentGate")
            .Produces<ParentGateResponse>();

        pin.MapPut("", async (
                PinBody body, ClaimsPrincipal user, HttpContext http, IClock clock,
                ICommandHandler<SetPinCommand, PinStatus> handler, CancellationToken ct) =>
            {
                var open = user.ParentAreaOpenUntil(clock.UtcNow) is not null;
                var result = await handler.HandleAsync(new SetPinCommand(user.AccountId(), body.Pin, open), ct);
                if (!result.IsSuccess) return result.ToHttp();
                var until = clock.UtcNow + Access.ParentAreaWindow;
                await ParentArea.ReissueSessionAsync(http, until);
                return Results.Ok(new ParentGateResponse(true, null, until));
            })
            .WithName("SetParentPin")
            .Produces<ParentGateResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        pin.MapPost("/unlock", async (
                PinBody body, HttpContext http, IClock clock,
                ICommandHandler<UnlockWithPinCommand, PinCheckView> handler, CancellationToken ct) =>
            {
                var result = await handler.HandleAsync(new UnlockWithPinCommand(http.User.AccountId(), body.Pin), ct);
                if (!result.IsSuccess) return result.ToHttp();
                var check = result.Value!;
                DateTimeOffset? until = null;
                if (check.Result == PinCheck.Unlocked)
                {
                    until = clock.UtcNow + Access.ParentAreaWindow;
                    await ParentArea.ReissueSessionAsync(http, until);
                }
                return Results.Ok(new UnlockResponse(check.Result, check.TriesLeft, check.LockedUntil, until));
            })
            .WithName("UnlockParentArea")
            .Produces<UnlockResponse>();

        pin.MapPost("/lock", async (HttpContext http) =>
            {
                await ParentArea.ReissueSessionAsync(http, null);
                return Results.NoContent();
            })
            .WithName("LockParentArea")
            .Produces(StatusCodes.Status204NoContent);

        return app;
    }
}
