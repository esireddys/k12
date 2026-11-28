using K12.Domain.Accounts;

namespace K12.Application.Accounts;

public sealed record KidView(Guid Id, string Name, AvatarColor Avatar, Guid? CourseId);

public sealed record AccountView(Guid Id, string DisplayName, string Email, IReadOnlyList<KidView> Kids);

internal static class AccountViews
{
    public static AccountView From(ParentAccount account) =>
        new(account.Id.Value, account.DisplayName, account.Email, account.Kids.Select(From).ToList());

    public static KidView From(KidProfile kid) => new(kid.Id.Value, kid.Name, kid.Avatar, kid.CourseId?.Value);
}
