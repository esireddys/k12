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

## Add a migration after changing a mapping

```powershell
dotnet ef migrations add <Name> --project src/K12.Infrastructure --startup-project src/K12.Api --output-dir Persistence/Migrations
```
