"""Command line entry point. Each subcommand becomes an Azure Container Apps job."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

import httpx

from .curriculum import course_payload, load_curriculum
from .extract import extract_items, render_item
from .forms import form_payload, form_slug
from .keys import load_key
from .sources import RELEASED_TESTS

DATA = Path(__file__).resolve().parents[2] / "data"


def _post(client: httpx.Client | None, path: str, body: dict) -> str:
    if client is None:
        return "not sent (--dry-run)"
    r = client.post(path, json=body)
    if r.status_code == 409:
        return "already imported"
    if r.is_error:
        raise SystemExit(f"{path} failed with {r.status_code}: {r.text}")
    return json.dumps(r.json())


def publish_released(client: httpx.Client | None) -> str:
    """Moves released questions an older import left in review to Live. Safe to repeat."""
    if client is None:
        return "not sent (--dry-run)"
    r = client.post("/api/admin/import/publish-released")
    if r.is_error:
        raise SystemExit(f"/api/admin/import/publish-released failed with {r.status_code}: {r.text}")
    return f"{r.json()['questionsPublished']} published"


def run_import(api: str, data: Path, dry_run: bool) -> None:
    out = data / "out"
    out.mkdir(parents=True, exist_ok=True)
    client = None if dry_run else httpx.Client(base_url=api, timeout=60, follow_redirects=True)

    for path in sorted((data / "curriculum").glob("*.json")):
        print(f"course {path.name}: {_post(client, '/api/admin/import/course', course_payload(load_curriculum(path)))}")

    for test in RELEASED_TESTS:
        slug = form_slug(test, test.subject)
        pdf = data / "raw" / test.pdf_name
        if not pdf.exists():
            print(f"skip {slug}: download {test.url} to {pdf}")
            continue
        items = extract_items(pdf, test.items)
        images = {}
        for item in items:
            rel = f"{slug}/{item.number:02}.png"
            render_item(pdf, item, out / rel)
            images[item.number] = rel
        payload = form_payload(test, test.subject, load_key(data / "keys" / f"{slug}.csv"), items, images)
        (out / f"{slug}.json").write_text(json.dumps(payload, indent=2, ensure_ascii=False), encoding="utf-8")
        print(f"form {slug} ({len(items)} items, pictures in {out / slug}): "
              f"{_post(client, '/api/admin/import/exam-form', payload)}")

    # Released questions go Live on import. This also catches any an older import left waiting in review.
    print(f"released questions left in review: {publish_released(client)}")


def main() -> None:
    parser = argparse.ArgumentParser(prog="k12-pipeline")
    sub = parser.add_subparsers(dest="command", required=True)
    sub.add_parser("sources", help="List the released tests the importer knows about")
    imp = sub.add_parser("import", help="Load curriculum and released tests into the API")
    imp.add_argument("--api", default="http://localhost:5143", help="API base URL (Development only for now)")
    imp.add_argument("--data", type=Path, default=DATA, help="Folder with curriculum, keys and raw")
    imp.add_argument("--dry-run", action="store_true", help="Build the payloads in data/out without sending them")
    pub = sub.add_parser("publish-released",
                         help="Make released questions that an older import left in review Live")
    pub.add_argument("--api", default="http://localhost:5143", help="API base URL (Development only for now)")
    rev = sub.add_parser("review", help="Run the AI reviewer over generated questions waiting for it")
    rev.add_argument("--api", default="http://localhost:5143", help="API base URL (Development only for now)")
    rev.add_argument("--images", type=Path, default=DATA / "out", help="Folder with the item pictures (data/out)")
    rev.add_argument("--limit", type=int, default=100, help="Review at most this many questions")
    rev.add_argument("--model", default=None, help="Claude model id (default: K12_REVIEW_MODEL or claude-opus-5-5)")
    rev.add_argument("--dry-run", action="store_true", help="Review and save JSON in data/out/reviews, send nothing")
    args = parser.parse_args()

    if args.command == "sources":
        for t in RELEASED_TESTS:
            print(f"{t.jurisdiction} {t.exam} {t.level} {t.year} {t.season} tagged={t.tagged} {t.url}")
    elif args.command == "import":
        run_import(args.api, args.data, args.dry_run)
    elif args.command == "publish-released":
        print(publish_released(httpx.Client(base_url=args.api, timeout=60, follow_redirects=True)))
    elif args.command == "review":
        from .review.reviewer import DEFAULT_MODEL
        from .review.runner import run_review

        run_review(args.api, args.images, args.limit, args.dry_run, args.model or DEFAULT_MODEL)
    else:
        raise SystemExit(f"'{args.command}' is not built yet.")


if __name__ == "__main__":
    main()
