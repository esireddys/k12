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

## Principles

- Content is data. States, standards, exams, skills and questions are rows, not code.
- Nothing reaches kids unreviewed. Every question passes the AI reviewer, then owner approval.
- Answer checking is exact and lives in the API (`api/src/K12.Core/Answers`). No AI runs while a kid answers.
