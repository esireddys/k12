"""Find the reading selections on a released reading test, and which items are about each one.

A reading test alternates: a page that starts "Read the selection ..." (or "Read the next two selections ..."),
the pages of the selection, then the items about it. Each selection becomes a passage: a picture of every page
(the faithful version students read) plus the text read from it, marked for checking like item text.
"""

from __future__ import annotations

import re
from dataclasses import dataclass, field
from pathlib import Path

import pymupdf

from .extract import FOOTER_TOP, ExtractedItem

# The instruction that opens every selection on the forms seen so far ("Read the selection and choose ...",
# "Read the next two selections and choose ...").
PASSAGE_START = re.compile(r"^\s*Read the (?:next \w+ )?selections?\b", re.IGNORECASE)
TITLE_MIN_SIZE = 18  # selection titles are set large and bold; body text is 11 to 12 points
FORM_ID = re.compile(r"^\d{4,5}(_\d)?$")  # form-building ids printed at the top of some pages
PAGE_TOP = 20


@dataclass
class ExtractedPassage:
    key: str  # unique within one test, for example "p1"
    title: str
    pages: list[int]  # 1-based page numbers
    text: str = ""
    items: list[int] = field(default_factory=list)


def _lines(page: pymupdf.Page) -> list[tuple[float, str, bool, float]]:
    """(top, text, bold, size) for every text line above the footer, top to bottom."""
    out = []
    for block in page.get_text("dict")["blocks"]:
        if block["type"] != 0:
            continue
        for line in block["lines"]:
            spans = [s for s in line["spans"] if s["text"].strip()]
            if not spans:
                continue
            top = line["bbox"][1]
            if top >= FOOTER_TOP:
                continue
            text = "".join(s["text"] for s in line["spans"]).strip()
            out.append((top, text, "Bold" in spans[0]["font"], max(s["size"] for s in spans)))
    return sorted(out, key=lambda x: x[0])


def find_passages(pdf: Path, items: list[ExtractedItem]) -> list[ExtractedPassage]:
    """Every selection in the test, with the items about it. Items before the first selection belong to none."""
    doc = pymupdf.open(pdf)
    item_pages = {i.page for i in items}
    starts = [
        n for n in range(1, doc.page_count + 1)
        if any(PASSAGE_START.match(t) for _, t, bold, _ in _lines(doc[n - 1]) if bold)
    ]

    passages: list[ExtractedPassage] = []
    for k, first in enumerate(starts):
        limit = starts[k + 1] if k + 1 < len(starts) else doc.page_count + 1
        pages = []
        for n in range(first, limit):
            if n in item_pages:
                break
            pages.append(n)
        lines = [ln for n in pages for ln in _lines(doc[n - 1])]
        titles = [t for _, t, bold, size in lines if bold and size >= TITLE_MIN_SIZE]
        body = [
            (t, bold) for _, t, bold, _ in lines
            if not (bold and PASSAGE_START.match(t)) and not t.lower().startswith("then fill in the answer")
            and not FORM_ID.match(t)
        ]
        passages.append(ExtractedPassage(
            key=f"p{k + 1}",
            title=" / ".join(titles) if titles else f"Selection {k + 1}",
            pages=pages,
            text=_clean_text(body),
        ))

    # Each item is about the last selection that starts before it.
    for item in items:
        owner = next((p for p in reversed(passages) if p.pages and p.pages[0] < item.page), None)
        if owner is not None:
            owner.items.append(item.number)

    empty = [p.key for p in passages if not p.items]
    if empty:
        raise ValueError(f"{pdf.name}: selections with no items after them: {', '.join(empty)}")
    return passages


def _clean_text(lines: list[tuple[str, bool]]) -> str:
    """Joins wrapped lines into paragraphs. A paragraph starts at a paragraph number or a bold heading."""
    out: list[str] = []
    previous_bold = True
    for line, bold in lines:
        text = re.sub(r"\s+", " ", line).strip()
        if not text:
            continue
        if out and not bold and not previous_bold and not re.match(r"^\d{1,2}\s", text):
            out[-1] = f"{out[-1]} {text}"
        else:
            out.append(text)
        previous_bold = bold
    return "\n".join(out)


def render_passage_pages(pdf: Path, passage: ExtractedPassage, out_dir: Path, zoom: float = 2.0) -> list[Path]:
    """Writes each page of the selection as a PNG (trimmed to its printed area) and returns the paths in order."""
    doc = pymupdf.open(pdf)
    paths = []
    for index, number in enumerate(passage.pages, start=1):
        page = doc[number - 1]
        clip = pymupdf.Rect(30, PAGE_TOP, 582, FOOTER_TOP) & page.rect
        marks = [pymupdf.Rect(r) for _, r in page.get_bboxlog()]
        inside = [r for r in marks if clip.y0 <= r.y0 < clip.y1 and r.height < 700 and r.width < 560]
        if inside:
            clip.y1 = min(clip.y1, max(r.y1 for r in inside) + 10)
        path = out_dir / f"{passage.key}-{index}.png"
        path.parent.mkdir(parents=True, exist_ok=True)
        page.get_pixmap(matrix=pymupdf.Matrix(zoom, zoom), clip=clip).save(path)
        paths.append(path)
    return paths
