import pytest

from k12_pipeline.sources import RELEASED_TESTS, SKIPPED_TESTS


def test_sources_point_at_tea():
    assert RELEASED_TESTS
    assert all(t.source_url.startswith("https://tea.texas.gov/") for t in RELEASED_TESTS)


def test_archived_copies_are_teas_own_files():
    archived = [t for t in RELEASED_TESTS if t.archived_from]
    assert archived
    for t in archived:
        assert t.url.startswith("https://web.archive.org/web/")
        assert t.url.endswith("id_/" + t.archived_from)  # the file exactly as TEA published it
        assert t.key_url.startswith("https://web.archive.org/web/")


def test_each_form_is_unique():
    keys = [(t.jurisdiction, t.exam, t.level, t.subject, t.language, t.year, t.season) for t in RELEASED_TESTS]
    assert len(keys) == len(set(keys))


def test_no_form_is_both_imported_and_skipped():
    imported = {(t.jurisdiction, t.exam, t.level, t.subject, t.year) for t in RELEASED_TESTS}
    skipped = {(s.jurisdiction, s.exam, s.level, s.subject, y) for s in SKIPPED_TESTS for y in s.years}
    assert not imported & skipped


@pytest.mark.parametrize(
    "level, subject, year",
    [("G3", "math", 2014), ("A1", "math", 2015), ("G3", "reading", 2019), ("A1", "math", 2023)],
)
def test_old_teks_and_online_only_forms_are_skipped(level, subject, year):
    assert any(s.level == level and s.subject == subject and year in s.years for s in SKIPPED_TESTS)


def test_browser_file_name_decodes_spaces():
    [a1_2016] = [t for t in RELEASED_TESTS if t.level == "A1" and t.year == 2016]
    assert a1_2016.pdf_name == "STAAR-EOC-2016Test-Algebra I-f.pdf"
