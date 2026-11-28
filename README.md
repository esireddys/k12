# K12 Practice

Math practice for K-12 students. It starts with Texas STAAR Grade 3, then Algebra I, then more states.
Kids practice by topic, in mixed sets, or with real released tests. Parents sign in with Google and add kid profiles.

| Folder | What | Stack |
| --- | --- | --- |
| `api/` | API, answer checking, question selection | ASP.NET Core (.NET 10, C#) |
| `web/` | Kid, parent and admin screens; PWA and App Store app | React + TypeScript + Vite, Capacitor |
| `pipeline/` | Imports released tests, generates variants, AI reviewer | Python |
| `infra/` | Azure resources | Bicep |

## Run locally

```bash
# API on http://localhost:5143
cd api && dotnet run --project src/K12.Api

# Web on http://localhost:5173
cd web && npm install && cp .env.example .env.local && npm run dev

# Pipeline
cd pipeline && pip install -e ".[dev]" && k12-pipeline sources
```

Parents sign in with Google once you set a Google client id (steps in [api/SIGN_IN.md](api/SIGN_IN.md)); until then,
the welcome screen offers a developer sign-in locally. Guests can practice without signing in.

## Deploy

Azure (Container Apps, Azure SQL serverless, Blob Storage, Key Vault), all Bicep in `infra/`, deployed by GitHub
Actions: **Infrastructure**, **Deploy** (image, database migrations, app) and **Import content** (released tests into
a deployed environment). One-time setup and costs: [infra/README.md](infra/README.md).

## Principles

- Content is data. States, standards, exams, skills and questions are rows, not code.
- Questions from official released tests go Live on import; the publisher already reviewed them.
  Generated (AI-written) questions reach kids only after the AI reviewer and then owner approval.
- Answer checking is exact and lives in the domain (`api/src/K12.Domain/Content/Answers`). No AI runs while a kid answers.

## Code structure

API (clean architecture, domain-driven design; dependencies point inward, enforced by `K12.Architecture.Tests`):

- `K12.Domain`: aggregates, value objects, domain services, repository interfaces. No framework code.
- `K12.Application`: use cases as command and query handlers, ports (`IClock`, `IUnitOfWork`).
- `K12.Infrastructure`: EF Core + SQL Server repositories (in-memory when no connection string is set), Azure, Claude. Local setup: [api/LOCAL_DATABASE.md](api/LOCAL_DATABASE.md).
- `K12.Api`: thin HTTP endpoints grouped by bounded context.

Web: `src/app` (routing, providers) → `src/features/<name>` → `src/shared` (UI kit, typed API client). A feature never imports another feature (ESLint `boundaries`). Regenerate the API client with `npm run gen:api` after an API change.
