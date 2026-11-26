# K12 Practice: notes for Claude

- Monorepo: `api/` (.NET 10, C#), `web/` (React + Vite + TS, Capacitor), `pipeline/` (Python), `infra/` (Bicep).
- Checks before a PR: `cd api && dotnet test`, `cd web && npm run lint && npm run build`, `cd pipeline && ruff check . && pytest -q`.
- Answer checking lives only in `api/src/K12.Core/Answers`. Every new answer kind or item type gets table-driven xUnit tests.
- Never hard-code a state, exam or standards name ("STAAR", "TEKS") in UI or API logic. Read it from curriculum data.
- Questions shown to kids must have status Live (AI reviewed and owner approved).
- Kid profiles hold no email, photo or location (children's privacy).
