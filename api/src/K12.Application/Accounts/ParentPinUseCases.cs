using K12.Application.Abstractions;
using K12.Domain.Accounts;
using K12.Domain.Common;

namespace K12.Application.Accounts;

/// <summary>Whether the parent has a PIN, and whether it is locked after wrong tries.</summary>
public sealed record GetPinStatusQuery(Guid AccountId);

public sealed record PinStatus(bool HasPin, DateTimeOffset? LockedUntil);

/// <param name="ParentAreaOpen">The parent areas are already open on this session (PIN or fresh sign-in).</param>
public sealed record SetPinCommand(Guid AccountId, string Pin, bool ParentAreaOpen);

public sealed record UnlockWithPinCommand(Guid AccountId, string Pin);

/// <param name="TriesLeft">Wrong tries left before the PIN locks.</param>
public sealed record PinCheckView(PinCheck Result, int TriesLeft, DateTimeOffset? LockedUntil);

public sealed class GetPinStatusHandler(IParentAccountRepository accounts, IClock clock) : IQueryHandler<GetPinStatusQuery, PinStatus>
{
    public async Task<Result<PinStatus>> HandleAsync(GetPinStatusQuery query, CancellationToken ct = default)
    {
        var account = await accounts.GetAsync(new ParentAccountId(query.AccountId), ct);
        if (account is null) return FamilyErrors.NoAccount;
        var pin = account.Pin;
        return new PinStatus(pin is not null, pin is not null && pin.IsLocked(clock.UtcNow) ? pin.LockedUntil : null);
    }
}

/// <summary>
/// Sets the first PIN, or changes it. A change needs the parent areas open, so a kid on a signed-in tablet
/// cannot replace the parent's PIN with their own.
/// </summary>
public sealed class SetPinHandler(IParentAccountRepository accounts, IPinHasher hasher, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<SetPinCommand, PinStatus>
{
    public async Task<Result<PinStatus>> HandleAsync(SetPinCommand command, CancellationToken ct = default)
    {
        var account = await accounts.GetAsync(new ParentAccountId(command.AccountId), ct);
        if (account is null) return FamilyErrors.NoAccount;
        if (account.HasPin && !command.ParentAreaOpen)
            return Error.Forbidden("parent.locked", "Enter your parent PIN first.");

        string pin;
        try
        {
            pin = ParentPin.Valid(command.Pin);
        }
        catch (DomainException e)
        {
            return Error.Validation("parent.bad_pin", e.Message);
        }

        account.SetPin(hasher.Hash(pin), clock.UtcNow);
        await unitOfWork.SaveChangesAsync(ct);
        return new PinStatus(true, null);
    }
}

/// <summary>Checks a typed PIN and counts wrong tries. The API opens the parent areas when it answers Unlocked.</summary>
public sealed class UnlockWithPinHandler(IParentAccountRepository accounts, IPinHasher hasher, IClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<UnlockWithPinCommand, PinCheckView>
{
    public async Task<Result<PinCheckView>> HandleAsync(UnlockWithPinCommand command, CancellationToken ct = default)
    {
        var account = await accounts.GetAsync(new ParentAccountId(command.AccountId), ct);
        if (account is null) return FamilyErrors.NoAccount;
        if (account.Pin is not { } current) return new PinCheckView(PinCheck.NoPin, ParentPin.MaxWrongTries, null);

        var now = clock.UtcNow;
        // A locked PIN is not even compared, so trying during the lock reveals nothing.
        var matches = !current.IsLocked(now) && IsWellFormed(command.Pin) && hasher.Verify(command.Pin.Trim(), current.Hash);
        var result = account.CheckPin(matches, now);
        await unitOfWork.SaveChangesAsync(ct);
        return new PinCheckView(
            result, Math.Max(0, ParentPin.MaxWrongTries - current.WrongTries), current.IsLocked(now) ? current.LockedUntil : null);
    }

    private static bool IsWellFormed(string? pin)
    {
        try
        {
            ParentPin.Valid(pin);
            return true;
        }
        catch (DomainException)
        {
            return false;
        }
    }
}
