using System.Security.Claims;
using K12.Api.Auth;
using K12.Application.Abstractions;
using K12.Application.Accounts;
using K12.Domain.Accounts;
using Microsoft.AspNetCore.Authentication;

namespace K12.Api.Endpoints;

/// <summary>
/// Sign-in and the family: a parent signs in with Google, the API keeps them signed in with its own cookie,
/// and the parent manages the kid profiles that practice under that sign-in. Guests use none of this.
/// </summary>
public static class AccountEndpoints
{
    public sealed record ProviderOption(string Provider, string ClientId);

    /// <summary>What the sign-in screen can offer: the providers set up on this server, and the local developer sign-in.</summary>
    public sealed record SignInOptions(IReadOnlyList<ProviderOption> Providers, bool DeveloperSignIn);

    public sealed record ProviderSignInBody(string Token);

    public sealed record DeveloperSignInBody(string Email, string? Name);

    /// <param name="Name">Display name, empty when nobody is signed in.</param>
    /// <param name="IsSignedIn">Anyone signed in, parent or (in Development) a header user.</param>
    /// <param name="HasFamily">A parent account with kid profiles stands behind the sign-in.</param>
    public sealed record MeResponse(string Name, string Email, bool IsSignedIn, bool HasFamily, bool IsOwner);

    public sealed record KidBody(string Name, AvatarColor Avatar, Guid? CourseId);

    public sealed record ChooseCourseBody(Guid CourseId);

    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app, IHostEnvironment environment)
    {
        app.MapGet("/api/me", (ClaimsPrincipal user) => Results.Ok(Me(user)))
            .WithTags("Account")
            .WithName("GetMe")
            .Produces<MeResponse>();

        var auth = app.MapGroup("/api/auth").WithTags("Account");

        auth.MapGet("/options", (IEnumerable<IExternalLoginVerifier> verifiers) => Results.Ok(new SignInOptions(
                verifiers.Where(v => v.IsConfigured).Select(v => new ProviderOption(v.Provider, v.ClientId!)).ToList(),
                environment.IsDevelopment())))
            .WithName("GetSignInOptions")
            .Produces<SignInOptions>();

        auth.MapPost("/{provider}", async (
                string provider, ProviderSignInBody body, HttpContext http,
                ICommandHandler<SignInWithProviderCommand, AccountView> handler, CancellationToken ct) =>
            await SignInAsync(http, await handler.HandleAsync(new SignInWithProviderCommand(provider, body.Token), ct)))
            .WithName("SignInWithProvider")
            .Produces<AccountView>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        if (environment.IsDevelopment())
        {
            // Registered before "/{provider}" matches it: literal segments win over parameters in routing.
            auth.MapPost("/developer", async (
                    DeveloperSignInBody body, HttpContext http,
                    ICommandHandler<SignInAsDeveloperCommand, AccountView> handler, CancellationToken ct) =>
                await SignInAsync(http, await handler.HandleAsync(new SignInAsDeveloperCommand(body.Email, body.Name), ct)))
                .WithName("SignInAsDeveloper")
                .Produces<AccountView>()
                .ProducesProblem(StatusCodes.Status400BadRequest);
        }

        auth.MapPost("/sign-out", async (HttpContext http) =>
            {
                await http.SignOutAsync(Access.SessionScheme);
                return Results.NoContent();
            })
            .WithName("SignOut")
            .Produces(StatusCodes.Status204NoContent);

        var family = app.MapGroup("/api/family").WithTags("Family").RequireAuthorization(Access.ParentPolicy);

        family.MapGet("", async (ClaimsPrincipal user, IQueryHandler<GetAccountQuery, AccountView> handler, CancellationToken ct) =>
            (await handler.HandleAsync(new GetAccountQuery(user.AccountId()), ct)).ToHttp())
            .WithName("GetFamily")
            .Produces<AccountView>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        family.MapPost("/kids", async (
                KidBody body, ClaimsPrincipal user, ICommandHandler<AddKidCommand, KidView> handler, CancellationToken ct) =>
            (await handler.HandleAsync(new AddKidCommand(user.AccountId(), body.Name, body.Avatar, body.CourseId), ct)).ToHttp())
            .WithName("AddKid")
            .RequireParentArea()
            .Produces<KidView>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        family.MapPut("/kids/{kidId:guid}", async (
                Guid kidId, KidBody body, ClaimsPrincipal user, ICommandHandler<UpdateKidCommand, KidView> handler, CancellationToken ct) =>
            (await handler.HandleAsync(new UpdateKidCommand(user.AccountId(), kidId, body.Name, body.Avatar, body.CourseId), ct)).ToHttp())
            .WithName("UpdateKid")
            .RequireParentArea()
            .Produces<KidView>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        family.MapPut("/kids/{kidId:guid}/course", async (
                Guid kidId, ChooseCourseBody body, ClaimsPrincipal user, IClock clock,
                ICommandHandler<ChooseKidCourseCommand, KidView> handler, CancellationToken ct) =>
            (await handler.HandleAsync(new ChooseKidCourseCommand(
                user.AccountId(), kidId, body.CourseId, user.ParentAreaOpenUntil(clock.UtcNow) is not null), ct)).ToHttp())
            .WithName("ChooseKidCourse")
            .Produces<KidView>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        family.MapDelete("/kids/{kidId:guid}", async (
                Guid kidId, ClaimsPrincipal user, ICommandHandler<RemoveKidCommand, AccountView> handler, CancellationToken ct) =>
            (await handler.HandleAsync(new RemoveKidCommand(user.AccountId(), kidId), ct)).ToHttp())
            .WithName("RemoveKid")
            .RequireParentArea()
            .Produces<AccountView>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static MeResponse Me(ClaimsPrincipal user)
    {
        var signedIn = user.Identity?.IsAuthenticated == true;
        return new MeResponse(
            signedIn ? user.Identity!.Name ?? "" : "",
            signedIn ? user.FindFirstValue(ClaimTypes.Email) ?? "" : "",
            signedIn,
            user.HasClaim(c => c.Type == Access.AccountIdClaim),
            user.IsInRole(Access.OwnerRole));
    }

    /// <summary>Turns a successful sign-in into the session cookie.</summary>
    private static async Task<IResult> SignInAsync(HttpContext http, Result<AccountView> result)
    {
        if (!result.IsSuccess) return result.ToHttp();
        var account = result.Value!;
        // Signing in again is how a parent who forgot the PIN gets back in, so a fresh sign-in opens the parent areas.
        var now = http.RequestServices.GetRequiredService<IClock>().UtcNow;
        await http.SignInAsync(Access.SessionScheme,
            Access.SessionPrincipal(account.Id, account.DisplayName, account.Email, now + Access.ParentAreaWindow),
            new AuthenticationProperties { IsPersistent = true });
        return Results.Ok(account);
    }
}
