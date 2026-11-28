"""Split a released-test PDF into numbered items.

Each item gets a picture of its region (the faithful version) plus the text read from the PDF's text layer.
The text is shown to students only where it is the whole of what is printed: a stem with no figure, emphasis,
fraction or unreadable glyph, or choices that are plain words and numbers. Everything else stays a picture.

The picture leaves out what belongs to the paper test, not to the question: the printed item number (the app
numbers questions itself), the "Record your answer and fill in the bubbles" line, form-building ids and the
side margins. When the choices are shown as text on the answer buttons, the picture stops above them.
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
UNREADABLE = "�"
FOOTER_TOP = 718  # page footers ("Mathematics / Page N") start below this on every form seen so far
MARGIN_LEFT = 110  # item numbers sit in the left gutter
GRIDDABLE_HINT = "Record your answer"
PAGE_NUMBER = re.compile(r"(?i)(?:^|\s)page\s*\d+$")
# Printed at the foot of a page but not part of an item: the arrow, the end-of-test notice, form-building ids.
END_NOTICE = re.compile(r"(?i)be sure you have recorded")
FOOTER_WORDS = re.compile(r"(?i)(?:go\s*on|stop|\d{5}(?:_\d)?)$|be sure you have recorded")
FORM_ID = re.compile(r"\d{5}(_\d)?")  # form-building ids printed above some items
# A stacked fraction reads from the text layer as numerator, the bar as "_", then the denominator.
STACKED_FRACTION = re.compile(r"(\d+) ?_ ?(\d+)")
MAX_CHOICE_TEXT = 200
# Pictures are drawn at this many pixels per PDF point. The app shows them at about 1.4 CSS pixels per point,
# so 12-point print reads at about 17 px; 3 keeps that sharp on a 2x tablet screen.
ZOOM = 3.0
PAD = 6.0  # points of white kept around the printed marks


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
    # The stem is the whole printed stem, as plain text: no figure, table, emphasis or fraction.
    stem_is_text: bool = False
    # Every choice is plain text and nothing else is drawn among them: the buttons can show the text.
    choices_are_text: bool = False
    # Where the parts sit on the page, for cropping the picture.
    number_box: tuple[float, float, float, float] | None = None
    choices_top: float | None = None
    record_top: float | None = None  # the "Record your answer" line on a griddable item
    form_id_boxes: list[tuple[float, float, float, float]] = field(default_factory=list)

    @property
    def bbox(self) -> tuple[float, float, float, float]:
        return (30.0, self.top, 582.0, self.bottom)

    @property
    def picture_bottom(self) -> float:
        """Where the picture stops: above text choices, above the answer-document line, or at the item's end."""
        if self.choices_are_text and self.choices_top is not None:
            return self.choices_top - 4
        if self.record_top is not None:
            return self.record_top - 4
        return self.bottom


@dataclass
class _Span:
    text: str
    font: str
    box: tuple[float, float, float, float]

    @property
    def emphasised(self) -> bool:
        return any(w in self.font for w in ("Bold", "Italic", "Oblique", "Black"))


@dataclass
class _Line:
    page: int
    x0: float
    y0: float
    x1: float
    y1: float
    text: str
    bold_start: bool
    spans: list[_Span] = field(default_factory=list)


def footer_top(page: pymupdf.Page) -> float:
    """Where the page footer ("Algebra I" over "Page 28") starts, from its page-number line. FOOTER_TOP when a page
    has no page number near the bottom."""
    lines = [
        (line["bbox"][0], line["bbox"][1], "".join(s["text"] for s in line["spans"]).strip())
        for block in page.get_text("dict")["blocks"] if block["type"] == 0
        for line in block["lines"]
    ]
    numbers = [(x0, y0) for x0, y0, text in lines if y0 >= FOOTER_TOP and PAGE_NUMBER.search(text)]
    if not numbers:
        return FOOTER_TOP
    _, y = min(numbers, key=lambda n: n[1])
    # The footer is the page-number line and the label printed just above or beside it ("Mathematics").
    return min(y0 for _, y0, text in lines if text and y - 20 <= y0 <= y + 2) - 1


def _lines(doc: pymupdf.Document) -> list[_Line]:
    out: list[_Line] = []
    for page_index, page in enumerate(doc):
        footer = footer_top(page)
        for block in page.get_text("dict")["blocks"]:
            if block["type"] != 0:
                continue
            for line in block["lines"]:
                spans = [s for s in line["spans"] if s["text"].strip()]
                if not spans:
                    continue
                x0, y0, x1, y1 = line["bbox"]
                # Below FOOTER_TOP is the page footer, except that some forms (Algebra I) print a low item's
                # "Record your answer" line there too: keep that line, it is how a gridded item is recognised.
                text = "".join(s["text"] for s in line["spans"])
                # Below FOOTER_TOP is usually the page footer, but some forms (Algebra I) print the end of a low
                # item there too (its "Record your answer" line, a last choice, an axis title). Keep those lines.
                if y0 >= FOOTER_TOP and (y0 >= footer or FOOTER_WORDS.match(text.strip())):
                    continue
                out.append(_Line(
                    page_index + 1, x0, y0, x1, y1, text, "Bold" in spans[0]["font"],
                    [_Span(s["text"], s["font"], tuple(s["bbox"])) for s in spans],
                ))
    return out


def _rows(lines: list[_Line]) -> list[_Line]:
    """Reading order, with pieces of one printed line joined. Some forms set a choice letter and its text as two
    lines on the same baseline, and the text can sort first; joining keeps the letter in front. Pieces far apart
    (choices side by side) stay separate."""
    out: list[_Line] = []
    for line in sorted(lines, key=lambda ln: (ln.page, ln.y0, ln.x0)):
        mid = (line.y0 + line.y1) / 2
        row = next(
            (r for r in reversed(out[-4:])
             if r.page == line.page and abs((r.y0 + r.y1) / 2 - mid) < 3
             and (0 <= line.x0 - r.x1 < 25 or 0 <= r.x0 - line.x1 < 25)),
            None,
        )
        if row is None:
            out.append(line)
            continue
        first, second = (row, line) if row.x0 <= line.x0 else (line, row)
        merged = _Line(
            row.page, first.x0, min(row.y0, line.y0), max(row.x1, line.x1), max(row.y1, line.y1),
            f"{first.text.rstrip()} {second.text.lstrip()}", first.bold_start, [*first.spans, *second.spans],
        )
        out[out.index(row)] = merged
    return sorted(out, key=lambda ln: (ln.page, ln.y0, ln.x0))


def _is_item_start(line: _Line, expected: int) -> bool:
    m = ITEM_NUMBER.match(line.text + " ")
    return bool(m) and line.bold_start and line.x0 < MARGIN_LEFT and int(m.group(1)) == expected


def _is_rule(r: pymupdf.Rect) -> bool:
    """The thin full-width line that separates items on the page."""
    return r.height < 3 and r.width > 300


def _figure_marks(page: pymupdf.Page, top: float, bottom: float) -> list[pymupdf.Rect]:
    """Everything drawn between top and bottom that is not text: figures, tables, underlines, pictures."""
    out = []
    for kind, box in page.get_bboxlog():
        if "text" in kind or kind.startswith(("clip", "ignore")):
            continue
        r = pymupdf.Rect(box)
        if top <= r.y0 < bottom and not r.is_empty and not _is_rule(r) and r.height < 600 and r.width < 560:
            out.append(r)
    return out


def extract_items(pdf: Path, expected_count: int) -> list[ExtractedItem]:
    """Finds items 1..expected_count in order. Raises if any are missing."""
    doc = pymupdf.open(pdf)
    lines = _rows(_lines(doc))

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
        # The end-of-test notice ("BE SURE YOU HAVE RECORDED...", in a box beside a stop sign) closes the last
        # item; it is not part of it.
        notice = [ln.y0 for ln in own if END_NOTICE.match(ln.text.strip())]
        own = [ln for ln in own if ln.y0 < min(notice, default=float("inf"))]
        # A figure-only tail can run past the last text line; take the page down to the next item or the footer.
        next_on_same_page = k + 1 < len(starts) and lines[starts[k + 1][0]].page == lines[start].page
        bottom = lines[starts[k + 1][0]].y0 - 12 if next_on_same_page else FOOTER_TOP
        low = [ln.y1 for ln in own if ln.y1 > FOOTER_TOP]  # the end of an item printed below FOOTER_TOP
        if not next_on_same_page and low:
            bottom = max(low) + 2
        if notice:
            bottom = min(bottom, min(notice) - 8)
        item = ExtractedItem(number, lines[start].page, max(lines[start].y0 - 14, 20), bottom)
        _read_text(item, own, doc[item.page - 1])
        items.append(item)
    return items


def _read_text(item: ExtractedItem, own: list[_Line], page: pymupdf.Page) -> None:
    stem_lines: list[_Line] = []
    choice_lines: dict[str, list[_Line]] = {}
    current: Choice | None = None
    labels = expected_labels(item.number)
    stem_emphasis = False
    for i, line in enumerate(own):
        text = line.text.strip()
        spans = line.spans
        if i == 0:
            text = ITEM_NUMBER.sub("", text + " ", count=1).strip()
            item.number_box = spans[0].box
            spans = spans[1:]
        if FORM_ID.fullmatch(text):
            item.form_id_boxes.append((line.x0, line.y0, line.x1, line.y1))
            continue
        if GRIDDABLE_HINT.lower() in text.lower() and item.record_top is None:
            item.record_top = line.y0
        m = CHOICE_LABEL.match(text) if line.bold_start and i > 0 else None
        taken = {c.label for c in item.choices}
        if m and m.group(1) in labels and m.group(1) not in taken:
            current = Choice(m.group(1), text[m.end():].strip())
            item.choices.append(current)
            choice_lines[current.label] = [line]
            if item.choices_top is None:
                item.choices_top = line.y0
        elif current is not None:
            current.text = f"{current.text} {text}".strip()
            choice_lines[current.label].append(line)
        else:
            stem_lines.append(line)
            stem_emphasis = stem_emphasis or any(s.emphasised for s in spans if s.text.strip())
    item.stem = _paragraphs(stem_lines, first_line_number=bool(own) and own[0] in stem_lines)
    for c in item.choices:
        c.text = _clean(c.text)
    item.choices.sort(key=lambda c: labels.index(c.label))
    item.griddable = GRIDDABLE_HINT.lower() in item.stem.lower()
    if item.griddable:
        item.stem = item.stem[: item.stem.lower().index(GRIDDABLE_HINT.lower())].strip()
        item.choices = []
        item.choices_top = None
    else:
        item.record_top = None

    if item.choices and item.choices_top is not None:
        choice_figures = _figure_marks(page, item.choices_top - 2, item.bottom)
        item.choices_are_text = not choice_figures and _choices_plain(item, choice_lines, labels)
        if item.choices_are_text:
            for c in item.choices:
                c.text = STACKED_FRACTION.sub(r"\1/\2", c.text)
    # The stem replaces the picture only when nothing in the whole item is drawn and the choices (if any) are text
    # too: a figure can sit between stem lines, and choices that are pictures mean the picture is needed anyway.
    item.stem_is_text = (
        bool(item.stem) and not stem_emphasis and _plain(item.stem)
        and not _figure_marks(page, item.top, item.bottom)
        and (item.griddable or item.choices_are_text)
    )


def _plain(text: str) -> bool:
    """Text a student can read as is: nothing unreadable and no stacked fraction (whose layout the text loses)."""
    return UNREADABLE not in text and "_" not in text and "￼" not in text


def _choices_plain(item: ExtractedItem, choice_lines: dict[str, list[_Line]], labels: str) -> bool:
    if [c.label for c in item.choices] != list(labels):
        return False
    columns = {round(lines[0].x0) for lines in choice_lines.values()}
    for c in item.choices:
        lines = choice_lines[c.label]
        if not c.text or len(c.text) > MAX_CHOICE_TEXT:
            return False
        # Side-by-side choices that wrap interleave their lines in reading order, so only single lines are safe.
        if len(columns) > 1 and len(lines) > 1 and not STACKED_FRACTION.fullmatch(c.text):
            return False
        # Emphasis (bold or italic words after the label) would be lost in plain text.
        body_spans = [s for ln in lines for s in ln.spans if s.text.strip()][1:]
        if any(s.emphasised for s in body_spans):
            return False
        text = c.text
        if "_" in text and not STACKED_FRACTION.fullmatch(text):
            return False
        if not _plain(STACKED_FRACTION.sub("", text)):
            return False
    return True


def _paragraphs(lines: list[_Line], first_line_number: bool) -> str:
    """Joins wrapped stem lines; a gap between lines or a bullet starts a new paragraph (one per line of output)."""
    out: list[str] = []
    previous: _Line | None = None
    for i, line in enumerate(lines):
        text = line.text.strip()
        if i == 0 and first_line_number:
            text = ITEM_NUMBER.sub("", text + " ", count=1).strip()
        text = _clean(text)
        if not text:
            continue
        gap = previous is not None and line.y0 - previous.y1 > 4
        if out and not gap and not text.startswith("•") and line.page == previous.page:  # type: ignore[union-attr]
            out[-1] = f"{out[-1]} {text}"
        else:
            out.append(text)
        previous = line
    return "\n".join(out)


def expected_labels(number: int) -> str:
    return LABELS_ODD if number % 2 else LABELS_EVEN


def _clean(text: str) -> str:
    # Symbol-font glyphs (some minus and division signs) come out as control characters; keep them visible.
    text = re.sub(r"[\x00-\x08\x0b-\x1f]", UNREADABLE, text)
    return re.sub(r"[ \t\r\f\v]+", " ", text).strip()


def _printed_area(page: pymupdf.Page, clip: pymupdf.Rect, skip: list[pymupdf.Rect]) -> pymupdf.Rect | None:
    """The box around everything printed inside clip, leaving out the boxes in skip and the item separator.
    Text is measured span by span (one drawing operation can cover a whole line, number included)."""
    boxes = [pymupdf.Rect(r) for r in _figure_marks(page, clip.y0, clip.y1)]
    for block in page.get_text("dict", clip=clip)["blocks"]:
        for line in block.get("lines", []):
            # A line that is one glyph whose text reads as blank is still printed: some forms (Algebra I) map
            # a graph's bold italic axis label to a space, and leaving it out would cut the label in half.
            lone = len(line["spans"]) == 1
            for span in line["spans"]:
                r = pymupdf.Rect(span["bbox"])
                printed = span["text"].strip() or (lone and span["text"] == " " and "BoldItalic" in span["font"])
                if printed and clip.y0 <= r.y0 < clip.y1 and not any(s.intersects(r) for s in skip):
                    boxes.append(r)
    area: pymupdf.Rect | None = None
    for r in boxes:
        area = r if area is None else area | r
    return area


def _render(page: pymupdf.Page, clip: pymupdf.Rect, blank: list[pymupdf.Rect], out: Path, zoom: float) -> Path:
    pix = page.get_pixmap(matrix=pymupdf.Matrix(zoom, zoom), clip=clip)
    for r in blank:
        # Pixmap coordinates are the page's, scaled: the clip's corner is not at 0, 0.
        box = pymupdf.IRect(int(r.x0 * zoom), int(r.y0 * zoom), int(r.x1 * zoom) + 1, int(r.y1 * zoom) + 1) & pix.irect
        if not box.is_empty:
            pix.set_rect(box, (255, 255, 255))
    out.parent.mkdir(parents=True, exist_ok=True)
    pix.save(out)
    return out


def render_item(pdf: Path, item: ExtractedItem, out: Path, zoom: float = ZOOM) -> Path:
    """Writes the item as a PNG, trimmed to what is printed and without the paper-test parts, and returns its path."""
    doc = pymupdf.open(pdf)
    page = doc[item.page - 1]
    clip = pymupdf.Rect(30.0, item.top, 582.0, item.picture_bottom) & page.rect
    blank = [pymupdf.Rect(b) + (-1, -1, 1, 1) for b in [item.number_box, *item.form_id_boxes] if b]
    area = _printed_area(page, clip, blank)
    if area is not None:
        clip = (area + (-PAD, -PAD, PAD, PAD)) & clip
    return _render(page, clip, blank, out, zoom)
