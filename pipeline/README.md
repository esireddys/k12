# pipeline

Offline Python jobs that build the question bank: import released tests, generate variants, run the AI reviewer.
Nothing here runs in a student's request. Every question still needs owner approval in the admin console.

```bash
python -m venv .venv && source .venv/bin/activate
pip install -e ".[dev]"
k12-pipeline sources
pytest
```
