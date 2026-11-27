from k12_pipeline.sources import RELEASED_TESTS


def test_sources_point_at_tea():
    assert RELEASED_TESTS
    assert all(t.url.startswith("https://tea.texas.gov/") for t in RELEASED_TESTS)


def test_each_form_is_unique():
    keys = [(t.jurisdiction, t.exam, t.level, t.subject, t.language, t.year, t.season) for t in RELEASED_TESTS]
    assert len(keys) == len(set(keys))
