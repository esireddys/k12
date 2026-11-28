"""Manual download helper for released test PDFs.

TEA does not allow automated downloads, so nothing here fetches a PDF. It works out which PDFs the importer
still needs, says where each one goes, and can open each official link in the user's own browser so they can
save it by hand.
"""

from __future__ import annotations

from collections.abc import Callable, Iterable
from dataclasses import dataclass
from pathlib import Path

from .sources import ReleasedTest


@dataclass(frozen=True)
class DownloadStatus:
    test: ReleasedTest
    # Where the PDF was found, or None when it still has to be downloaded.
    found: Path | None
    # Where a new download should be saved.
    save_to: Path

    @property
    def missing(self) -> bool:
        return self.found is None

    @property
    def label(self) -> str:
        t = self.test
        return f"{t.jurisdiction} {t.exam} {t.level} {t.subject} {t.year}"


def check_downloads(tests: Iterable[ReleasedTest], raw: Path) -> list[DownloadStatus]:
    """Each test with the PDF already on disk (any known name, new or old folder layout) or where to save it."""
    return [DownloadStatus(t, t.find_pdf(raw), t.expected_path(raw)) for t in tests]


def format_report(statuses: list[DownloadStatus], show_all: bool = False) -> str:
    missing = [s for s in statuses if s.missing]
    lines: list[str] = []
    for s in statuses if show_all else missing:
        if s.missing:
            lines += [f"MISSING  {s.label}", f"    open    {s.test.url}", f"    save to {s.save_to}"]
        else:
            lines += [f"ok       {s.label}", f"    found   {s.found}"]
    have = len(statuses) - len(missing)
    if not missing:
        lines.append(f"All {len(statuses)} PDFs are downloaded. Next: k12-pipeline import")
    else:
        lines.append(f"{have} of {len(statuses)} PDFs downloaded, {len(missing)} still needed.")
        lines.append("Save each PDF with its file name unchanged. Then run: k12-pipeline import")
    return "\n".join(lines)


def open_missing(statuses: list[DownloadStatus], open_url: Callable[[str], object]) -> int:
    """Makes each missing PDF's folder and opens its official link with `open_url` (the user's browser).
    Returns how many links were opened."""
    opened = 0
    for s in statuses:
        if s.missing:
            s.save_to.parent.mkdir(parents=True, exist_ok=True)
            open_url(s.test.url)
            opened += 1
    return opened
