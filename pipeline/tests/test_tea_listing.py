import pytest

from k12_pipeline.sources import RELEASED_TESTS, SKIPPED_TESTS, ReleasedTest, SkippedTest
from k12_pipeline.tea_listing import Listing, compare, format_check, parse_listing

BASE = "https://tea.texas.gov/data-reports/staar/released-test-questions/"


def _page(*names: str) -> str:
    return "".join(f'<li><a href="{BASE}{n}">x</a></li>' for n in names)


@pytest.mark.parametrize(
    "name, expected",
    [
        ("2022-staar-may-grade-3-math-releasedtest.pdf", Listing("G3", "math", 2022, True)),
        ("2021-staar-gr3-mathematics-test-tagged.pdf", Listing("G3", "math", 2021, True)),
        ("2025-staar-math-3-answer-key.pdf", Listing("G3", "math", 2025, False)),
        ("2024-staar-3-rla-answer-key.pdf", Listing("G3", "reading", 2024, False)),
        ("2021-staar-gr3-reading-test.pdf", Listing("G3", "reading", 2021, True)),
        ("2026-staar-algebra-1-answer-key.pdf", Listing("A1", "math", 2026, False)),
        ("may-2022-staar-algebra-i-answerkey-final-2.pdf", Listing("A1", "math", 2022, False)),
        ("STAAR-G3-2016Test-Math-f.pdf", Listing("G3", "math", 2016, True)),
    ],
)
def test_reads_our_courses_from_file_names(name, expected):
    assert parse_listing(_page(name)) == [expected]


@pytest.mark.parametrize(
    "name",
    [
        "2022-staar-may-spanish-grade-3-math-releasedtest.pdf",  # not English
        "2023-staar-math-grade-3-rationale.pdf",                 # not a form
        "2023-staar-grade-3-math-student-expectations.xlsx",
        "2023-staar-3-math-item-sampler.pdf",                    # sample questions, not a released form
        "may-2022-staar-math-4-answerkey-final-2.pdf",           # another grade
        "2022-staar-may-geometry-releasedtest.pdf",
    ],
)
def test_ignores_other_files(name):
    assert parse_listing(_page(name)) == []


def test_test_and_key_for_one_year_are_one_listing():
    page = _page("2022-staar-may-grade-3-math-releasedtest.pdf", "may-2022-staar-math-3-answerkey-final-2.pdf")
    assert parse_listing(page) == [Listing("G3", "math", 2022, True)]


def test_reports_years_neither_imported_nor_skipped():
    known = [ReleasedTest("TX", "STAAR", "G3", 2022, "spring", BASE + "a.pdf", tagged=False)]
    skipped = [SkippedTest("TX", "STAAR", "G3", "math", (2023,), "online")]
    listings = [Listing("G3", "math", y, y == 2022) for y in (2022, 2023, 2027)]
    check = compare(listings, known, skipped)
    assert [x.year for x in check.imported] == [2022]
    assert [x.year for x, _ in check.skipped] == [2023]
    assert [x.year for x in check.new] == [2027]
    assert "NEW      G3 math 2027: answer key only" in format_check(check)


def test_todays_tea_page_has_nothing_new():
    # File names from TEA's released-tests page as of November 2026, one per course and year.
    page = _page(
        *[f"{y}-staar-3-math-answer-key.pdf" for y in (2023, 2024, 2026)], "2025-staar-math-3-answer-key.pdf",
        *[f"{y}-staar-3-rla-answer-key.pdf" for y in (2024, 2026)], "2025-staar-rla-3-answer-key.pdf",
        *[f"{y}-staar-algebra-1-answer-key.pdf" for y in (2023, 2025, 2026)], "2024-staar-algebra-i-answer-key.pdf",
        "2022-staar-may-grade-3-math-releasedtest.pdf", "2021-staar-gr3-mathematics-test-tagged.pdf",
        "2019-staar-3-math-test.pdf", "2022-staar-may-grade-3-reading-releasedtest.pdf",
        "2021-staar-gr3-reading-test.pdf", "2019-staar-3-reading-test.pdf",
        "2022-staar-may-algebra-i-releasedtest.pdf", "2021-staar-algebra-i-test-tagged.pdf",
        "2019-staar-algebra-i-test.pdf",
    )
    check = compare(parse_listing(page), RELEASED_TESTS, SKIPPED_TESTS)
    assert check.new == []
    assert len(check.imported) == 8 and len(check.skipped) == 12
