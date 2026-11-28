"""Joins a released test's extracted items with its answer key into the exam form import payload."""

from __future__ import annotations

import re
from typing import Any

from .extract import ExtractedItem
from .keys import KeyRow
from .passages import ExtractedPassage
from .sources import ReleasedTest


def form_slug(test: ReleasedTest, subject: str) -> str:
    return f"{test.jurisdiction}-{test.exam}-{test.level}-{subject}-{test.year}".lower()


GRIDDED_KINDS = {"Integer", "Decimal"}

_UNIT = (
    r"(?:square |cubic )?(?:feet|foot|inch(?:es)?|yards?|miles?|(?:centi|milli|kilo)?meters?|units?"
    r"|(?:kilo)?grams?|pounds?|ounces?|(?:milli)?liters?|cups?|pints?|quarts?|gallons?"
    r"|seconds?|minutes?|hours?|days?|weeks?|months?|years?|dollars?|cents?|degrees(?: Fahrenheit| Celsius)?)"
)
_UNIT_PHRASE = re.compile(rf"\bin ({_UNIT})(?=[\s,?.]|$)", re.IGNORECASE)


def answer_unit(stem: str) -> str | None:
    """The unit a typed answer is asked in ("What is the area in square feet?"), the last one the stem names."""
    found = _UNIT_PHRASE.findall(stem or "")
    return found[-1].lower() if found else None


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
    test: ReleasedTest, subject: str, key: list[KeyRow], items: list[ExtractedItem], image_for: dict[int, str],
    passages: list[ExtractedPassage] | None = None, pages_for: dict[str, list[str]] | None = None,
) -> dict[str, Any]:
    """The body for POST /api/admin/import/exam-form. Raises when the key and the PDF disagree."""
    if len(key) != len(items):
        raise ValueError(f"key has {len(key)} items, PDF has {len(items)}")
    passages = passages or []
    passage_of = {n: p.key for p in passages for n in p.items}
    if test.has_passages and len(passage_of) != len(items):
        missing = [i.number for i in items if i.number not in passage_of]
        raise ValueError(f"items with no selection before them: {missing}")

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
            # Paragraphs on their own lines. Read from the PDF's text layer.
            "stem": item.stem,
            # The stem text is shown instead of the picture only when it is the whole printed stem (no figure,
            # emphasis or fraction). Otherwise the picture is what students read.
            "textNeedsReview": not item.stem_is_text,
            "showImage": not item.stem_is_text,
        }
        if row.is_choice:
            content["choices"] = [{"label": c.label, "text": c.text} for c in item.choices]
            # The answer buttons show the choice text, and the picture stops above the choices.
            content["choicesAreText"] = item.choices_are_text
        elif unit := answer_unit(item.stem):
            # Shown beside the answer box, so a kid knows "64" means 64 square feet.
            content["unit"] = unit
        entry: dict[str, Any] = {
            "number": row.item,
            "skillCodes": [row.skill],
            "itemType": "choice" if row.is_choice else "numeric",
            "content": content,
            "answer": _answer(row, test.gridded_kind),
            # The key's reporting category for this item. A reading skill counts as literary or informational
            # depending on the selection, so the item's category is kept rather than read from its skill.
            "category": row.category,
        }
        if row.item in passage_of:
            entry["passage"] = passage_of[row.item]
        out.append(entry)

    payload: dict[str, Any] = {
        "jurisdiction": test.jurisdiction,
        "subject": subject,
        "courseCode": test.level,
        "exam": test.exam,
        "year": test.year,
        "season": test.season,
        "sourceUrl": test.url,
        "language": test.language,
        "items": out,
    }
    if passages:
        payload["passages"] = [
            {
                "key": p.key,
                "title": p.title,
                # The page pictures are what students read; the text is for search and read-aloud once checked.
                "content": {"pages": (pages_for or {}).get(p.key, []), "text": p.text, "textNeedsReview": True},
            }
            for p in passages
        ]
    return payload
