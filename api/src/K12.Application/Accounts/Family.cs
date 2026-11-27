using K12.Application.Abstractions;
using K12.Domain.Accounts;
using K12.Domain.Common;
using K12.Domain.Curriculum;

namespace K12.Application.Accounts;

public sealed record GetAccountQuery(Guid AccountId);

public sealed record AddKidCommand(Guid AccountId, string Name, AvatarColor Avatar, Guid? CourseId);

public sealed record UpdateKidCommand(Guid AccountId, Guid KidId, string Name, AvatarColor Avatar, Guid? CourseId);

/// <summary>Sets only the kid's course, from the course picker a kid sees.</summary>
public sealed record ChooseKidCourseCommand(Guid AccountId, Guid KidId, Guid CourseId);

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
            account.ChooseCourse(id, courseId!.Value);
            return account.Kids.Single(k => k.Id == id);
        });
}

public sealed class RemoveKidHandler(IParentAccountRepository accounts, IUnitOfWork unitOfWork)
    : ICommandHandler<RemoveKidCommand, AccountView>
{
    public async Task<Result<AccountView>> HandleAsync(RemoveKidCommand command, CancellationToken ct = default)
    {
        var account = await accounts.GetAsync(new ParentAccountId(command.AccountId), ct);
        if (account is null) return FamilyErrors.NoAccount;
        var id = new KidProfileId(command.KidId);
        if (!account.HasKid(id)) return FamilyErrors.NoKid;
        account.RemoveKid(id);
        await unitOfWork.SaveChangesAsync(ct);
        return AccountViews.From(account);
    }
}

internal static class FamilyErrors
{
    public static readonly Error NoAccount = Error.NotFound("family.no_account", "Sign in again: your account was not found.");
    public static readonly Error NoKid = Error.NotFound("family.no_kid", "That kid is not in your family.");
    public static readonly Error NoCourse = Error.Validation("family.no_course", "That course does not exist.");
}

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
        catch (DomainException e)
        {
            return Error.Validation("family.invalid", e.Message);
        }
    }
}
