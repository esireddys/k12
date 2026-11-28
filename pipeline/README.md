# pipeline

Offline Python jobs that build the question bank: import released tests, generate variants, run the AI reviewer.
Nothing here runs in a student's request. Questions from released tests go Live on import; generated
questions need the AI reviewer and then owner approval in the admin console.

Windows Command Prompt, from `C:\code\k12\pipeline`:

```bat
python -m venv .venv
.venv\Scripts\activate
pip install -e ".[dev]"
k12-pipeline sources
pytest
```

macOS or Linux: `source .venv/bin/activate` instead of the second line. A plain `pip install -e .` installs
everything the importer and the AI reviewer need; `[dev]` adds pytest and ruff.

## Downloading the test PDFs

TEA does not allow automated downloads, so the PDFs are saved by hand. The helper says which ones are still missing,
gives each official link and the folder to save it in, and never downloads anything itself:

```bat
k12-pipeline downloads          :: what is still missing, with link and folder
k12-pipeline downloads --all    :: also lists the PDFs already in data\raw
k12-pipeline downloads --open   :: opens each missing link in your browser and makes its folder
```

Keep the file name the browser gives (the helper also knows the renamed copies TEA sometimes serves), then run
`k12-pipeline import`. The list comes from `sources.py`; add a row there for each new released test.

Which years are imported (all in `sources.py`):

- Grade 3 Math and Algebra I: 2016 to 2019, 2021 and 2022. TEA took 2018 and older off its site, so those links
  go to the Internet Archive's copy of TEA's original file; the import still records TEA's own link as the source.
  The 2016 Grade 3 form has 46 items; the others have 32 (Grade 3) or 54 (Algebra I).
- Grade 3 Reading: 2021 and 2022.
- Left out on purpose (`SKIPPED_TESTS`): math before 2016 and reading before 2021 test older TEKS whose skill codes
  mean something else today, and 2023 on are online tests with new item types (TEA posts only their answer keys).
  No STAAR was given in 2020.

To see whether TEA has posted a year we neither import nor skip (it reads the page's links only, never a PDF):

```bat
k12-pipeline downloads --check-tea
:: if TEA refuses the request, save the page from your browser (HTML only) and pass it instead:
k12-pipeline downloads --check-tea --page "%USERPROFILE%\Downloads\STAAR Released Test Questions.html"
```

## Importing released tests

What lives where:

- `data/curriculum/*.json`: courses, reporting categories and skills (short paraphrased descriptions, fraction limits).
- `data/keys/*.csv`: the published answer key for each test, one row per item.
- `data/raw/<state>/<subject>/`: the test PDFs, for example `data\raw\tx\reading\`. Download each URL that
  `k12-pipeline sources` lists into the folder it names. PDFs straight in `data/raw/` (the older layout) still work.
  Not committed (publisher copyright).
- `data/out/`: what the import makes: one picture per item and the JSON it sends. Not committed.

With the API running locally in Development (`dotnet run --project src/K12.Api` from `api/`):

```bash
k12-pipeline import            # sends the course, then each test whose PDF is in data/raw
k12-pipeline import --dry-run  # builds data/out without sending anything
```

Running it again is safe: the course and skills are updated in place, and a test that is already
imported is refreshed: its questions take the newer pictures and text (same ids, status and kids' progress),
except ones an owner edited, which are kept. The import checks that the key and the PDF agree (item count, multiple choice
versus griddable, and that each answer letter is one of the choices) before it sends anything.

What students see from an item: the text read from the PDF is shown only where it is the whole of what is
printed. A stem with no figure, emphasis or fraction is shown as text instead of the picture; choices that are
plain words, numbers or simple fractions go on the answer buttons and the picture stops above them. Everything
else stays a picture. Pictures leave out the printed item number, the "Record your answer and fill in the
bubbles" line, form ids and the side margins, and are drawn at 3 pixels per point. Selection pages leave out the
"Then fill in the answer on your answer document" instruction.

Reading tests also get their selections: each "Read the selection ..." page starts a passage, every page of it is
saved as a picture (`p1-1.png`, `p1-2.png`, ...) and the items after it are linked to it, so students read the
selection beside its questions. Each item also keeps the reporting category from the answer key, because a reading
skill counts as literary or informational depending on the selection.

Each curriculum file can describe the real test's shape under `practiceTest` (items, minutes, items per reporting
category). The app builds its full practice test from that: a fresh mix of released questions every time.

Every course is English for now (`"language": "en"`); a Spanish test would be its own course and curriculum file.

Imported questions are Official and go Live straight away: the publisher already reviewed them, so they
skip the AI reviewer and the owner queue. The picture of each item is the source of truth; the text read
from the PDF is marked `textNeedsReview` because it can be rough.

### Questions imported before this rule

Imports made before released questions skipped review left them waiting in AI review. Either of these moves
them Live (both are safe to run again; rejected or retired questions are left alone):

```bat
k12-pipeline import
k12-pipeline publish-released
```

`import` refreshes the tests that are already in and then publishes any leftovers; `publish-released` only does the second part.

## Running the AI reviewer

The reviewer is for generated questions only; released questions never wait for it.
It sends each question waiting in AI review to Claude with its picture, the extracted text, the
answer key and its skill. Claude solves the item from the picture, rewrites the text exactly as printed,
describes any figure, writes a short explanation and suggests a difficulty. Plain code then double-checks
the answer against the key and the choice letters, and decides the verdict. Every question then waits for
an owner in the web app (`/owner/review`); nothing reaches students until an owner approves it.

Needs the API running in Development, the pictures in `data/out` from `k12-pipeline import`, and an
Anthropic API key in the `ANTHROPIC_API_KEY` environment variable. Never put the key in a file in the repo.

```bat
set ANTHROPIC_API_KEY=your-key-here
k12-pipeline review --limit 3 --dry-run   # reviews 3, writes data/out/reviews/*.json, sends nothing
k12-pipeline review                       # reviews everything waiting and sends the results
```

The model defaults to `claude-opus-5-5`; pass `--model` or set `K12_REVIEW_MODEL` to change it.
If the reviewer cannot judge an item (no picture, or the model declines), the item still goes to the
owners, flagged, with its text unchanged. A question an owner is already looking at is skipped.
