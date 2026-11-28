"""`k12-pipeline review`: works through the API's AI review queue one question at a time."""

from __future__ import annotations

import json
import os
import time
from pathlib import Path
from typing import Any

import httpx

from .reviewer import DEFAULT_MODEL, AiReviewer, ReviewerUnavailable, build_payload, unreviewable_payload


def _client() -> Any:
    try:
        import anthropic
    except ImportError as e:  # pragma: no cover - depends on the install
        raise SystemExit("The AI reviewer needs the anthropic package. From pipeline/, run: pip install -e .") from e
    # Reads ANTHROPIC_API_KEY (or an `ant auth login` profile). Never put the key in code or in git.
    return anthropic.Anthropic(max_retries=4)


def run_review(api: str, images: Path, limit: int, dry_run: bool, model: str = DEFAULT_MODEL,
               reviewer: AiReviewer | None = None, http: httpx.Client | None = None) -> dict[str, int]:
    # Locally the API lets a request act as a named user through X-Dev-User; Access:ReviewAgents grants the role.
    agent = os.environ.get("K12_REVIEW_AGENT", "review-agent@localhost")
    http = http or httpx.Client(base_url=api, timeout=60, follow_redirects=True, headers={"X-Dev-User": agent})
    r = http.get("/api/admin/review/ai-queue", params={"take": limit})
    if r.is_error:
        raise SystemExit(f"Could not read the AI review queue ({r.status_code}): {r.text}")
    items = r.json()["items"]
    print(f"{len(items)} question(s) waiting for AI review")
    if not items:
        return {"reviewed": 0, "flagged": 0, "skipped": 0}

    reviewer = reviewer or AiReviewer(_client(), model)
    out_dir = images / "reviews"
    out_dir.mkdir(parents=True, exist_ok=True)
    stats = {"reviewed": 0, "flagged": 0, "skipped": 0}

    for item in items:
        src = item.get("source") or {}
        label = f"{src.get('exam', '?')} {src.get('year', '?')} item {src.get('itemNumber', '?')}"
        image_path = (item.get("content") or {}).get("image")
        started = time.monotonic()
        if image_path and (images / image_path).is_file():
            try:
                payload = build_payload(item, reviewer.review(item, (images / image_path).read_bytes(), image_path))
            except ReviewerUnavailable as e:
                payload = unreviewable_payload(item, str(e), model)
        else:
            payload = unreviewable_payload(item, f"its picture {image_path!r} is not in {images}", model)

        (out_dir / f"{item['id']}.json").write_text(json.dumps(payload, indent=2, ensure_ascii=False), encoding="utf-8")
        flag = payload["verdict"] == "NeedsAttention"
        verdict = "needs attention" if flag else "looks good"
        took = f"{time.monotonic() - started:.0f}s"
        if dry_run:
            print(f"{label}: {verdict} ({took}, not sent: --dry-run)")
            stats["reviewed"] += 1
            stats["flagged"] += flag
            continue
        sent = http.post(f"/api/admin/review/questions/{item['id']}/ai-review", json=payload)
        if sent.status_code == 409:
            print(f"{label}: skipped, the question changed or was already reviewed")
            stats["skipped"] += 1
            continue
        if sent.is_error:
            raise SystemExit(f"{label}: saving the review failed with {sent.status_code}: {sent.text}")
        print(f"{label}: {verdict} ({took}) - {payload['summary']}")
        stats["reviewed"] += 1
        stats["flagged"] += flag

    print(f"done: {stats['reviewed']} reviewed, {stats['flagged']} flagged for a closer look, {stats['skipped']} skipped")
    return stats
