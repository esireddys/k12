"""Split a released-test PDF into numbered items.

Each item gets a picture of its whole region (the faithful version students see first) plus a
best-effort text reading of the stem and choices. Stacked fractions and figures do not survive
text extraction, so the text is marked for review and the picture stays the source of truth.
"""

from __future__ import annotations

import re
from dataclasses import dataclass, field
from pathlib import Path

import pymupdf

ITEM_NUMBER = re.compile(r"^\s*(\d{1,2})\s")
CHOICE_LABEL = re.compile(r"^\s*([A-DFGHJ])(?:\s|$)")
# Released tests alternate choice letters so a student cannot slip a row on the answer sheet.
LABELS_ODD, LABELS_EVEN = "ABCD", "FGHJ"
UNREADABLE = "\ufffd"
FOOTER_TOP = 718  # page footers ("Mathematics / Page N") start below this on every form seen so far
MARGIN_LEFT = 110  # item numbers sit in the left gutter
GRIDDABLE_HINT = "Record your answer"


@dataclass
class Choice:
    label: str
    text: str


@dataclass
class ExtractedItem:
    number: int
    page: int  # 1-based
    top: float
    bottom: float
    stem: str = ""
    choices: list[Choice] = field(default_factory=list)
    griddable: bool = False

    @property
    def bbox(self) -> tuple[float, float, float, float]:
        return (30.0, self.top, 582.0, self.bottom)


@dataclass
class _Line:
    page: int
    x0: float
    y0: float
    y1: float
    text: str
    bold_start: bool


def _lines(doc: pymupdf.Document) -> list[_Line]:
    out: list[_Line] = []
    for page_index, page in enumerate(doc):
        for block in page.get_text("dict")["blocks"]:
            if block["type"] != 0:
                continue
            for line in block["lines"]:
                spans = [s for s in line["spans"] if s["text"].strip()]
                if not spans:
                    continue
                x0, y0, _, y1 = line["bbox"]
                if y0 >= FOOTER_TOP:
                    continue
                text = "".join(s["text"] for s in line["spans"])
                out.append(_Line(page_index + 1, x0, y0, y1, text, "Bold" in spans[0]["font"]))
    return out


def _is_item_start(line: _Line, expected: int) -> bool:
    m = ITEM_NUMBER.match(line.text + " ")
    return bool(m) and line.bold_start and line.x0 < MARGIN_LEFT and int(m.group(1)) == expected


def extract_items(pdf: Path, expected_count: int) -> list[ExtractedItem]:
    """Finds items 1..expected_count in order. Raises if any are missing."""
    doc = pymupdf.open(pdf)
    lines = sorted(_lines(doc), key=lambda ln: (ln.page, ln.y0, ln.x0))

    starts: list[tuple[int, int]] = []  # (line index, item number)
    expected = 1
    for i, line in enumerate(lines):
        if expected <= expected_count and _is_item_start(line, expected):
            starts.append((i, expected))
            expected += 1
    if expected != expected_count + 1:
        raise ValueError(f"{pdf.name}: found items 1..{expected - 1}, expected {expected_count}")

    items: list[ExtractedItem] = []
    for k, (start, number) in enumerate(starts):
        end = starts[k + 1][0] if k + 1 < len(starts) else len(lines)
        own = [ln for ln in lines[start:end] if ln.page == lines[start].page]
        # A figure-only tail can run past the last text line; take the page down to the next item or the footer.
        next_on_same_page = k + 1 < len(starts) and lines[starts[k + 1][0]].page == lines[start].page
        bottom = lines[starts[k + 1][0]].y0 - 12 if next_on_same_page else FOOTER_TOP
        item = ExtractedItem(number, lines[start].page, max(lines[start].y0 - 14, 20), bottom)
        _read_text(item, own)
        items.append(item)
    return items


def _read_text(item: ExtractedItem, own: list[_Line]) -> None:
    stem: list[str] = []
    current: Choice | None = None
    labels = expected_labels(item.number)
    for i, line in enumerate(own):
        text = line.text.strip()
        if i == 0:
            text = ITEM_NUMBER.sub("", text + " ", count=1).strip()
        if re.fullmatch(r"\d{5}(_\d)?", text):  # form-building ids printed above some items
            continue
        m = CHOICE_LABEL.match(text) if line.bold_start and i > 0 else None
        taken = {c.label for c in item.choices}
        if m and m.group(1) in labels and m.group(1) not in taken:
            current = Choice(m.group(1), text[m.end():].strip())
            item.choices.append(current)
        elif current is not None:
            current.text = f"{current.text} {text}".strip()
        else:
            stem.append(text)
    item.stem = _clean(" ".join(s for s in stem if s))
    for c in item.choices:
        c.text = _clean(c.text)
    item.choices.sort(key=lambda c: labels.index(c.label))
    item.griddable = GRIDDABLE_HINT.lower() in item.stem.lower()
    if item.griddable:
        item.stem = item.stem[: item.stem.lower().index(GRIDDABLE_HINT.lower())].strip()
        item.choices = []


def expected_labels(number: int) -> str:
    return LABELS_ODD if number % 2 else LABELS_EVEN


def _clean(text: str) -> str:
    # Symbol-font glyphs (some minus and division signs) come out as control characters; keep them visible.
    text = re.sub(r"[\x00-\x08\x0b-\x1f]", UNREADABLE, text)
    return re.sub(r"\s+", " ", text).strip()


def render_item(pdf: Path, item: ExtractedItem, out: Path, zoom: float = 2.0) -> Path:
    """Writes the item's region as a PNG and returns its path."""
    doc = pymupdf.open(pdf)
    page = doc[item.page - 1]
    clip = pymupdf.Rect(*item.bbox) & page.rect
    # Trim the blank space under the item: stop just below the lowest text or drawing that starts in it.
    marks = [pymupdf.Rect(r) for _, r in page.get_bboxlog()]
    inside = [r for r in marks if clip.y0 <= r.y0 < clip.y1 and r.height < 600 and r.width < 560]
    if inside:
        clip.y1 = min(clip.y1, max(r.y1 for r in inside) + 10)
    out.parent.mkdir(parents=True, exist_ok=True)
    page.get_pixmap(matrix=pymupdf.Matrix(zoom, zoom), clip=clip).save(out)
    return out
