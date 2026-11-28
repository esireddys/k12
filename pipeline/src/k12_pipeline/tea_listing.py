"""Compares TEA's released-tests page with the tests the importer knows about.

It only reads the page's links. Nothing is downloaded: a year TEA lists that we neither import nor skip on
purpose is reported so someone can decide whether to add it to `sources.py`.
"""

from __future__ import annotations

import re
from collections.abc import Iterable
from dataclasses import dataclass
from html.parser import HTMLParser
from urllib.parse import unquote

from .sources import ReleasedTest, SkippedTest

# The courses we import, as TEA spells them in file names: level -> subject -> pattern.
_COURSES: dict[tuple[str, str], re.Pattern[str]] = {
    ("G3", "math"): re.compile(r"(grade-?3|gr-?3|\bg3|-3-)[^/]*math|math[^/]*(grade-?3|-3\b)"),
    ("G3", "reading"): re.compile(r"(grade-?3|gr-?3|\bg3|-3-)[^/]*(reading|rla)|(reading|rla)[^/]*(grade-?3|-3\b)"),
    ("A1", "math"): re.compile(r"algebra-?(i|1)\b|alg-?1\b"),
}
_YEAR = re.compile(r"(?<!\d)(20\d\d)(?!\d)")
_KEY = re.compile(r"answer-?key|-key\b|key-")
_TEST = re.compile(r"releasedtest|-test\b|\dtest-|test-tagged")
# Not a released form, or not English.
_IGNORE = re.compile(r"spanish|rationale|expectation|exptested|sampler|practice|redesign|writing")


@dataclass(frozen=True)
class Listing:
    level: str
    subject: str
    year: int
    has_test: bool  # a test PDF is listed, not only its answer key

    @property
    def label(self) -> str:
        return f"{self.level} {self.subject} {self.year}"


class _Links(HTMLParser):
    def __init__(self) -> None:
        super().__init__()
        self.hrefs: list[str] = []

    def handle_starttag(self, tag: str, attrs: list[tuple[str, str | None]]) -> None:
        if tag == "a":
            href = dict(attrs).get("href")
            if href:
                self.hrefs.append(href)


def parse_listing(html: str) -> list[Listing]:
    """The released forms for our courses that the page links to, one per course and year."""
    parser = _Links()
    parser.feed(html)
    found: dict[tuple[str, str, int], bool] = {}
    for href in parser.hrefs:
        name = unquote(href).rsplit("/", 1)[-1].lower().replace("_", "-").replace(" ", "-")
        if not name.endswith(".pdf") or _IGNORE.search(name):
            continue
        is_test, is_key = bool(_TEST.search(name)), bool(_KEY.search(name))
        year = _YEAR.search(name)
        if not year or not (is_test or is_key):
            continue
        for (level, subject), pattern in _COURSES.items():
            if pattern.search(name):
                k = (level, subject, int(year.group(1)))
                found[k] = found.get(k, False) or is_test
    return [Listing(level, subject, year, has_test) for (level, subject, year), has_test in sorted(found.items())]


@dataclass(frozen=True)
class ListingCheck:
    new: list[Listing]  # on TEA's page, neither imported nor skipped on purpose
    imported: list[Listing]
    skipped: list[tuple[Listing, str]]


def compare(listings: Iterable[Listing], known: Iterable[ReleasedTest], skipped: Iterable[SkippedTest]) -> ListingCheck:
    have = {(t.level, t.subject, t.year) for t in known}
    why = {(s.level, s.subject, y): s.reason for s in skipped for y in s.years}
    new, imported, left_out = [], [], []
    for item in listings:
        k = (item.level, item.subject, item.year)
        if k in have:
            imported.append(item)
        elif k in why:
            left_out.append((item, why[k]))
        else:
            new.append(item)
    return ListingCheck(new, imported, left_out)


def format_check(check: ListingCheck) -> str:
    total = len(check.imported) + len(check.skipped) + len(check.new)
    lines = [(f"TEA's page lists {total} released forms for our courses: "
              f"{len(check.imported)} imported, {len(check.skipped)} left out on purpose.")]
    if not check.new:
        lines.append("Nothing new on TEA's page.")
    for item in check.new:
        kind = "test PDF and key" if item.has_test else "answer key only (the test is online)"
        lines.append(f"NEW      {item.label}: {kind}. Decide whether to add it to sources.py.")
    return "\n".join(lines)
