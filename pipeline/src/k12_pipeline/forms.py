"""Joins a released test's extracted items with its answer key into the exam form import payload."""

from __future__ import annotations

from typing import Any

from .extract import ExtractedItem
from .keys import KeyRow
from .sources import ReleasedTest


def form_slug(test: ReleasedTest, subject: str) -> str:
    return f"{test.jurisdiction}-{test.exam}-{test.level}-{subject}-{test.year}".lower()


GRIDDED_KINDS = {"Integer", "Decimal"}


def _answer(row: KeyRow, gridded_kind: str | None) -> dict[str, Any]:
    if row.is_choice:
        return {"kind": "Choice", "value": row.answer}
    if gridded_kind is None:
        return {"kind": "Decimal" if "." in row.answer else "Integer", "value": row.answer}
    if gridded_kind not in GRIDDED_KINDS:
        raise ValueError(f"gridded answers are {' or '.join(sorted(GRIDDED_KINDS))}, not {gridded_kind}")
    if gridded_kind == "Integer" and "." in row.answer:
        raise ValueError(f"item {row.item}: key {row.answer} is not a whole number")
    return {"kind": gridded_kind, "value": row.answer}


def form_payload(
    test: ReleasedTest, subject: str, key: list[KeyRow], items: list[ExtractedItem], image_for: dict[int, str]
) -> dict[str, Any]:
    """The body for POST /api/admin/import/exam-form. Raises when the key and the PDF disagree."""
    if len(key) != len(items):
        raise ValueError(f"key has {len(key)} items, PDF has {len(items)}")

    out = []
    for row, item in zip(key, items, strict=True):
        if row.item != item.number:
            raise ValueError(f"key row {row.item} lines up with PDF item {item.number}")
        if row.is_choice == item.griddable:
            kind = "multiple choice" if row.is_choice else "griddable"
            raise ValueError(f"item {row.item}: key says {kind}, PDF says otherwise")
        if row.is_choice and row.answer not in {c.label for c in item.choices}:
            raise ValueError(f"item {row.item}: answer {row.answer} is not one of the choices")

        content: dict[str, Any] = {
            "image": image_for[item.number],
            "stem": item.stem,
            # Read from the PDF, so the text may be rough; the picture is the source of truth.
            "textNeedsReview": True,
        }
        if row.is_choice:
            content["choices"] = [{"label": c.label, "text": c.text} for c in item.choices]
        out.append({
            "number": row.item,
            "skillCodes": [row.skill],
            "itemType": "choice" if row.is_choice else "numeric",
            "content": content,
            "answer": _answer(row, test.gridded_kind),
        })

    return {
        "jurisdiction": test.jurisdiction,
        "subject": subject,
        "courseCode": test.level,
        "exam": test.exam,
        "year": test.year,
        "season": test.season,
        "sourceUrl": test.url,
        "items": out,
    }
