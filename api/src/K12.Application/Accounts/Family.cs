using K12.Application.Abstractions;
using K12.Domain.Accounts;
using K12.Domain.Common;
using K12.Domain.Curriculum;
using K12.Domain.Progress;

namespace K12.Application.Accounts;

public sealed record GetAccountQuery(Guid AccountId);

public sealed record AddKidCommand(Guid AccountId, string Name, AvatarColor Avatar, Guid? CourseId);

public sealed record UpdateKidCommand(Guid AccountId, Guid KidId, string Name, AvatarColor Avatar, Guid? CourseId);

/// <summary>
/// Sets only the kid's course, from the course picker a kid sees. A kid may pick their first course; changing it
/// afterwards is a parent's call, so it needs the parent areas open once the parent has set a PIN.
/// </summary>
public sealed record ChooseKidCourseCommand(Guid AccountId, Guid KidId, Guid CourseId, bool ParentAreaOpen = false);

public sealed record RemoveKidCommand(Guid AccountId, Guid KidId);

/// <summary>The signed-in parent's account and kids.</summary>
public sealed class GetAccountHandler(IParentAccountRepository accounts) : IQueryHandler<GetAccountQuery, AccountView>
{
    public async Task<Result<AccountView>> HandleAsync(GetAccountQuery query, CancellationToken ct = default)
    {
        var account = await accounts.GetAsync(new ParentAccountId(query.AccountId), ct);
        return account is null ? FamilyErrors.NoAccount : AccountViews.From(account);
    }
}

public sealed class AddKidHandler(IParentAccountRepository accounts, ICourseRepository courses, IUnitOfWork unitOfWork)
    : ICommandHandler<AddKidCommand, KidView>
{
    public Task<Result<KidView>> HandleAsync(AddKidCommand command, CancellationToken ct = default) =>
        FamilyChange.RunAsync(accounts, courses, unitOfWork, command.AccountId, command.CourseId, ct,
            (account, courseId) => account.AddKid(command.Name, command.Avatar, courseId));
}

public sealed class UpdateKidHandler(IParentAccountRepository accounts, ICourseRepository courses, IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateKidCommand, KidView>
{
    public Task<Result<KidView>> HandleAsync(UpdateKidCommand command, CancellationToken ct = default) =>
        FamilyChange.RunAsync(accounts, courses, unitOfWork, command.AccountId, command.CourseId, ct, (account, courseId) =>
        {
            var id = new KidProfileId(command.KidId);
            if (!account.HasKid(id)) return null;
            return account.UpdateKid(id, command.Name, command.Avatar, courseId);
        });
}

public sealed class ChooseKidCourseHandler(IParentAccountRepository accounts, ICourseRepository courses, IUnitOfWork unitOfWork)
    : ICommandHandler<ChooseKidCourseCommand, KidView>
{
    public Task<Result<KidView>> HandleAsync(ChooseKidCourseCommand command, CancellationToken ct = default) =>
        FamilyChange.RunAsync(accounts, courses, unitOfWork, command.AccountId, command.CourseId, ct, (account, courseId) =>
        {
            var id = new KidProfileId(command.KidId);
            if (!account.HasKid(id)) return null;
            var kid = account.Kids.Single(k => k.Id == id);
            if (kid.CourseId is { } current && current != courseId && account.HasPin && !command.ParentAreaOpen)
                throw new ParentAreaLockedException();
            account.ChooseCourse(id, courseId!.Value);
            return kid;
        });
}

/// <summary>Removes a kid and forgets everything saved about them: their history and progress go too.</summary>
public sealed class RemoveKidHandler(
    IParentAccountRepository accounts, IKidProgressRepository progress, IPracticeSessionRepository sessions, IUnitOfWork unitOfWork)
    : ICommandHandler<RemoveKidCommand, AccountView>
{
    public async Task<Result<AccountView>> HandleAsync(RemoveKidCommand command, CancellationToken ct = default)
    {
        var account = await accounts.GetAsync(new ParentAccountId(command.AccountId), ct);
        if (account is null) return FamilyErrors.NoAccount;
        var id = new KidProfileId(command.KidId);
        if (!account.HasKid(id)) return FamilyErrors.NoKid;
        account.RemoveKid(id);
        if (await progress.GetAsync(id, ct) is { } kidProgress) progress.Remove(kidProgress);
        await sessions.RemoveAllForKidAsync(id, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return AccountViews.From(account);
    }
}

internal static class FamilyErrors
{
    public static readonly Error NoAccount = Error.NotFound("family.no_account", "Sign in again: your account was not found.");
    public static readonly Error NoKid = Error.NotFound("family.no_kid", "That kid is not in your family.");
    public static readonly Error NoCourse = Error.Validation("family.no_course", "That course does not exist.");
    public static readonly Error ParentLocked = Error.Forbidden("parent.locked", "Ask a parent: changing the grade needs the parent PIN.");
}

/// <summary>Thrown inside a family change that needs the parent areas open when they are not.</summary>
internal sealed class ParentAreaLockedException : Exception;

/// <summary>Loads the account, checks the course exists, applies one change, saves.</summary>
internal static class FamilyChange
{
    public static async Task<Result<KidView>> RunAsync(
        IParentAccountRepository accounts, ICourseRepository courses, IUnitOfWork unitOfWork,
        Guid accountId, Guid? courseId, CancellationToken ct, Func<ParentAccount, CourseId?, KidProfile?> change)
    {
        var account = await accounts.GetAsync(new ParentAccountId(accountId), ct);
        if (account is null) return FamilyErrors.NoAccount;

        CourseId? course = courseId is { } c ? new CourseId(c) : null;
        if (course is { } id && (await courses.ListByIdsAsync([id], ct)).Count == 0) return FamilyErrors.NoCourse;

        try
        {
            var kid = change(account, course);
            if (kid is null) return FamilyErrors.NoKid;
            await unitOfWork.SaveChangesAsync(ct);
            return AccountViews.From(kid);
        }
        catch (ParentAreaLockedException)
        {
            return FamilyErrors.ParentLocked;
        }
        catch (DomainException e)
        {
            return Error.Validation("family.invalid", e.Message);
        }
    }
}
