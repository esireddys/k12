# pipeline

Offline Python jobs that build the question bank: import released tests, generate variants, run the AI reviewer.
Nothing here runs in a student's request. Every question still needs owner approval in the admin console.

```bash
python -m venv .venv && source .venv/bin/activate   # Windows: .venv\Scripts\activate
pip install -e ".[dev,ai]"
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

## Running the AI reviewer

The reviewer sends each question waiting in AI review to Claude with its picture, the extracted text, the
answer key and its skill. Claude solves the item from the picture, rewrites the text exactly as printed,
describes any figure, writes a short explanation and suggests a difficulty. Plain code then double-checks
the answer against the key and the choice letters, and decides the verdict. Every question then waits for
an owner in the web app (`/owner/review`); nothing reaches students until an owner approves it.

Needs the API running in Development, the pictures in `data/out` from `k12-pipeline import`, and an
Anthropic API key in the `ANTHROPIC_API_KEY` environment variable. Never put the key in a file in the repo.

```bash
pip install -e ".[ai]"
k12-pipeline review --limit 3 --dry-run   # reviews 3, writes data/out/reviews/*.json, sends nothing
k12-pipeline review                       # reviews everything waiting and sends the results
```

The model defaults to `claude-opus-5-5`; pass `--model` or set `K12_REVIEW_MODEL` to change it.
If the reviewer cannot judge an item (no picture, or the model declines), the item still goes to the
owners, flagged, with its text unchanged. A question an owner is already looking at is skipped.
