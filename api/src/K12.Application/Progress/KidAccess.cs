using K12.Application.Abstractions;
using K12.Domain.Accounts;
using K12.Domain.Curriculum;

namespace K12.Application.Progress;

/// <summary>The signed-in parent's kid that a progress use case is about.</summary>
internal sealed record FamilyKid(ParentAccount Account, KidProfile Kid);

internal static class KidAccess
{
    public static readonly Error NoAccount = Error.NotFound("family.no_account", "Sign in again: your account was not found.");
    public static readonly Error NoKid = Error.NotFound("family.no_kid", "That kid is not in your family.");
    public static readonly Error NoCourse = Error.Validation("progress.no_course", "Pick a grade for this kid first.");

    /// <summary>Loads the account and checks the kid is in it. A parent only ever reaches their own kids.</summary>
    public static async Task<Result<FamilyKid>> LoadAsync(
        IParentAccountRepository accounts, Guid accountId, Guid kidId, CancellationToken ct)
    {
        var account = await accounts.GetAsync(new ParentAccountId(accountId), ct);
        if (account is null) return NoAccount;
        var kid = account.Kids.FirstOrDefault(k => k.Id == new KidProfileId(kidId));
        return kid is null ? NoKid : new FamilyKid(account, kid);
    }

    /// <summary>The kid's own date: what their device says, else the server's (UTC) date.</summary>
    public static DateOnly Today(DateOnly? localDate, IClock clock) => localDate ?? DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);

    /// <summary>The skill a question counts toward in a course: its first skill there, else its first skill.</summary>
    public static SkillId? SkillIn(IReadOnlyCollection<SkillId> questionSkills, IReadOnlySet<SkillId> courseSkills) =>
        questionSkills.Where(courseSkills.Contains).Cast<SkillId?>().FirstOrDefault() ?? questionSkills.Cast<SkillId?>().FirstOrDefault();
}
