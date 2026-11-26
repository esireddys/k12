# pipeline

Offline Python jobs that build the question bank: import released tests, generate variants, run the AI reviewer.
Nothing here runs in a student's request. Every question still needs owner approval in the admin console.

```bash
python -m venv .venv && source .venv/bin/activate   # Windows: .venv\Scripts\activate
pip install -e ".[dev]"
k12-pipeline sources
pytest
```

## Importing released tests

What lives where:

- `data/curriculum/*.json`: courses, reporting categories and skills (short paraphrased descriptions, fraction limits).
- `data/keys/*.csv`: the published answer key for each test, one row per item.
- `data/raw/`: the test PDFs. Download each URL that `k12-pipeline sources` lists. Not committed (publisher copyright).
- `data/out/`: what the import makes: one picture per item and the JSON it sends. Not committed.

With the API running locally in Development (`dotnet run --project src/K12.Api` from `api/`):

```bash
k12-pipeline import            # sends the course, then each test whose PDF is in data/raw
k12-pipeline import --dry-run  # builds data/out without sending anything
```

Running it again is safe: the course and skills are updated in place, and a test that is already
imported is skipped. The import checks that the key and the PDF agree (item count, multiple choice
versus griddable, and that each answer letter is one of the choices) before it sends anything.

Imported questions are Official and wait in AI review. Students see none of them until an owner approves each one.
The picture of each item is the source of truth; the text read from the PDF is marked for the AI reviewer to clean up.
