from pathlib import Path

import pytest

from k12_pipeline.downloads import check_downloads, format_report, open_missing
from k12_pipeline.sources import RELEASED_TESTS, ReleasedTest

MATH = ReleasedTest("TX", "STAAR", "G3", 2022, "spring", "https://tea.texas.gov/x/g3-math.pdf", tagged=False)
READING = ReleasedTest("TX", "STAAR", "G3", 2021, "spring", "https://tea.texas.gov/x/g3-reading.pdf",
                       tagged=False, subject="reading", also_named=("g3-reading-2.pdf",))


def _touch(path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(b"%PDF")


@pytest.mark.parametrize(
    "on_disk, missing",
    [
        ([], True),
        (["tx/math/g3-math.pdf"], False),   # the layout to use
        (["g3-math.pdf"], False),           # older flat layout
        (["tx/reading/g3-math.pdf"], True),  # wrong subject folder does not count
    ],
)
def test_finds_math_pdf(tmp_path, on_disk, missing):
    for rel in on_disk:
        _touch(tmp_path / rel)
    [status] = check_downloads([MATH], tmp_path)
    assert status.missing is missing
    assert status.save_to == tmp_path / "tx" / "math" / "g3-math.pdf"


def test_renamed_copy_counts_as_downloaded(tmp_path):
    _touch(tmp_path / "tx" / "reading" / "g3-reading-2.pdf")
    [status] = check_downloads([READING], tmp_path)
    assert not status.missing


def test_report_lists_only_missing_by_default(tmp_path):
    _touch(tmp_path / "tx" / "math" / "g3-math.pdf")
    report = format_report(check_downloads([MATH, READING], tmp_path))
    assert "g3-reading.pdf" in report and READING.url in report
    assert "ok " not in report
    assert "1 of 2 PDFs downloaded, 1 still needed." in report


def test_report_all_downloaded(tmp_path):
    _touch(tmp_path / "tx" / "math" / "g3-math.pdf")
    report = format_report(check_downloads([MATH], tmp_path), show_all=True)
    assert report.startswith("ok ")
    assert "All 1 PDFs are downloaded" in report


def test_open_missing_opens_links_and_makes_folders_only(tmp_path):
    _touch(tmp_path / "tx" / "math" / "g3-math.pdf")
    opened: list[str] = []
    count = open_missing(check_downloads([MATH, READING], tmp_path), opened.append)
    assert count == 1 and opened == [READING.url]
    assert (tmp_path / "tx" / "reading").is_dir()
    assert not (tmp_path / "tx" / "reading" / "g3-reading.pdf").exists()  # nothing is downloaded


def test_every_known_test_has_a_save_folder(tmp_path):
    for status in check_downloads(RELEASED_TESTS, tmp_path):
        assert status.save_to.parent.parent.name == status.test.jurisdiction.lower()
        assert status.save_to.name.endswith(".pdf")
