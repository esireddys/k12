# Local SQL Server

The API uses SQL Server when `ConnectionStrings:K12` is set, and an in-memory store when it isn't.

## Set the connection string (once per machine)

Keep it in user secrets, not in appsettings, so nothing machine-specific is committed:

```powershell
cd api
# SQL Server Express (named instance SQLEXPRESS), Windows sign-in:
dotnet user-secrets set "ConnectionStrings:K12" "Server=localhost\SQLEXPRESS;Database=K12;Trusted_Connection=True;TrustServerCertificate=True" --project src/K12.Api
# or a default instance (SQL Server Developer):
#   "Server=localhost;Database=K12;Trusted_Connection=True;TrustServerCertificate=True"
# or LocalDB:
#   "Server=(localdb)\MSSQLLocalDB;Database=K12;Trusted_Connection=True"
```

## Create the database

Running the API in Development applies migrations and adds one sample question:

```powershell
dotnet run --project src/K12.Api
```

Or apply migrations yourself:

```powershell
dotnet tool restore
dotnet ef database update --project src/K12.Infrastructure --startup-project src/K12.Api
```

Then open SSMS, connect to `localhost\SQLEXPRESS` with Windows Authentication (tick "Trust server certificate"), and look at `K12` > Tables: `content.Questions` and `curriculum.Skills`.

`content.Questions` also holds the review trail: `AiReviewJson` (the AI reviewer's findings and proposed text)
and `OwnerOutcome`, `OwnerDecidedBy`, `OwnerDecidedAt`, `OwnerNote` (the owner's decision).

## Owners and item pictures (Development)

Sign in on the welcome screen as an owner to use the review screens at http://localhost:5173/owner/review: either the
**Developer sign-in** as `owner@localhost`, or Google with an email you added to `Access:Owners` (see
[SIGN_IN.md](SIGN_IN.md)). Scripts can send the header `X-Dev-User: someone@example.org` to act as someone without a
cookie; outside Development neither the developer sign-in nor the header exists.

Parent accounts and kid profiles are in `accounts.ParentAccounts` and `accounts.KidProfiles`. The parent PIN is stored
only as a salted hash (`PinHash`), with `PinWrongTries` and `PinLockedUntil` for the lock after five wrong tries.

A kid's saved practice is in the `progress` schema: `PracticeSessions` and `SessionAnswers` (every set and answer),
`SkillProgress` (mastery per skill), `Mistakes` (missed questions for "Fix my mistakes"; `FixedAt` is set once answered
right) and `PracticeDays` (for streaks and the weekly view). Guests never write here. Removing a kid deletes their rows.

Item pictures are served from `Content:ImageRoot`, which defaults to `../../../pipeline/data/out`
(relative to `src/K12.Api`). Override it with user secrets if your pictures live elsewhere.

## Add a migration after changing a mapping

```powershell
dotnet ef migrations add <Name> --project src/K12.Infrastructure --startup-project src/K12.Api --output-dir Persistence/Migrations
```
