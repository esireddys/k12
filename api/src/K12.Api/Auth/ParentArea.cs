using System.Security.Claims;
using K12.Application.Abstractions;
using K12.Application.Accounts;
using Microsoft.AspNetCore.Authentication;

namespace K12.Api.Auth;

/// <summary>
/// The parent areas (the dashboard, managing kids, changing the PIN) sit behind the parent PIN, so a kid on a
/// shared tablet with the parent signed in cannot open them. Until the parent sets a PIN, they stay open.
/// </summary>
public static class ParentArea
{
    public const string LockedCode = "parent.locked";

    /// <summary>Lets the request through only when the parent areas are open on this session, or no PIN is set.</summary>
    public static RouteHandlerBuilder RequireParentArea(this RouteHandlerBuilder builder) =>
        builder.AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
            var user = http.User;
            var clock = http.RequestServices.GetRequiredService<IClock>();
            if (user.ParentAreaOpenUntil(clock.UtcNow) is not null) return await next(context);

            var status = await http.RequestServices.GetRequiredService<IQueryHandler<GetPinStatusQuery, PinStatus>>()
                .HandleAsync(new GetPinStatusQuery(user.AccountId()), http.RequestAborted);
            if (status.IsSuccess && !status.Value!.HasPin) return await next(context);

            return Results.Problem(title: LockedCode, detail: "Enter your parent PIN first.", statusCode: StatusCodes.Status403Forbidden);
        }).ProducesProblem(StatusCodes.Status403Forbidden);

    /// <summary>Re-issues the session cookie with the parent areas open until <paramref name="until"/>, or closed when null.</summary>
    public static Task ReissueSessionAsync(HttpContext http, DateTimeOffset? until)
    {
        var user = http.User;
        return http.SignInAsync(Access.SessionScheme,
            Access.SessionPrincipal(user.AccountId(), user.Identity?.Name ?? "", user.FindFirstValue(ClaimTypes.Email) ?? "", until),
            new AuthenticationProperties { IsPersistent = true });
    }
}
