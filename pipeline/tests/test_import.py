import re
from pathlib import Path

import pymupdf
import pytest

from k12_pipeline.curriculum import course_payload, load_curriculum
from k12_pipeline.extract import extract_items, render_item
from k12_pipeline.forms import form_payload, form_slug
from k12_pipeline.keys import KeyRow, load_key
from k12_pipeline.sources import RELEASED_TESTS, ReleasedTest

DATA = Path(__file__).resolve().parents[1] / "data"
TEST = ReleasedTest("TX", "EXAM", "G3", 2021, "spring", "https://example.org/t.pdf", tagged=False)


def _pdf(tmp_path: Path) -> Path:
    """A two-item test laid out like the released forms: bold numbers in the gutter, bold choice letters."""
    doc = pymupdf.open()
    page = doc.new_page(width=612, height=792)
    lines = [
        (60, 100, "1 Which number is greatest?", True),
        (75, 130, "A 12", True), (75, 150, "B 21", True), (75, 170, "C 9", True), (75, 190, "D 20", True),
        (60, 300, "2 What is 4 x 5?", True),
        (75, 320, "Record your answer and fill in the bubbles.", False),
    ]
    for x, y, text, bold in lines:
        page.insert_text((x, y), text, fontname="hebo" if bold else "helv", fontsize=11)
    page.insert_text((36, 760), "Page 1", fontname="helv", fontsize=10)
    path = tmp_path / "t.pdf"
    doc.save(path)
    return path


def _curricula() -> dict[tuple[str, str, str], dict]:
    docs = [load_curriculum(p) for p in sorted((DATA / "curriculum").glob("*.json"))]
    return {(d["jurisdiction"], d["subject"], d["level"]): d for d in docs}


def test_every_released_test_has_a_course_and_a_key():
    curricula = _curricula()
    for test in RELEASED_TESTS:
        assert (test.jurisdiction, test.subject, test.level) in curricula, test
        assert len(load_key(DATA / "keys" / f"{form_slug(test, test.subject)}.csv")) == test.items, test


def test_every_key_file_matches_the_curriculum():
    curricula = _curricula()
    for test in RELEASED_TESTS:
        skills = {s["code"]: s for s in curricula[(test.jurisdiction, test.subject, test.level)]["skills"]}
        for row in load_key(DATA / "keys" / f"{form_slug(test, test.subject)}.csv"):
            assert row.skill in skills, row
            assert skills[row.skill]["category"] == row.category, row
            assert skills[row.skill]["designation"] == row.designation, row


def test_algebra_keys_have_five_gridded_items_each_that_can_be_negative_or_decimal():
    algebra = [t for t in RELEASED_TESTS if t.level == "A1"]
    assert sorted(t.year for t in algebra) == [2019, 2021, 2022]
    for test in algebra:
        gridded = [r for r in load_key(DATA / "keys" / f"{form_slug(test, test.subject)}.csv") if not r.is_choice]
        assert [r.item for r in gridded] == [14, 20, 27, 34, 42], test
        assert all(re.fullmatch(r"-?\d+(\.\d+)?", r.answer) for r in gridded), test
    answers = {r.answer for t in algebra for r in load_key(DATA / "keys" / f"{form_slug(t, t.subject)}.csv")}
    assert {"-254", "-0.2", "12.3"} <= answers


def test_algebra_is_a_course_usually_taken_in_grade_9_and_grade_3_is_a_grade():
    algebra = course_payload(load_curriculum(DATA / "curriculum" / "tx-math-a1.json"))
    grade3 = course_payload(load_curriculum(DATA / "curriculum" / "tx-math-g3.json"))
    assert (algebra["code"], algebra["name"], algebra["kind"], algebra["typicalGrade"]) == ("A1", "Algebra I", "course", 9)
    assert (grade3["kind"], grade3["typicalGrade"]) == ("grade", 3)
    assert len(algebra["categories"]) == 5


@pytest.mark.parametrize(
    ("change", "message"),
    [({"kind": "semester"}, "grade or course"), ({"typicalGrade": 13}, "0 \\(kindergarten\\) to 12"),
     ({"kind": "grade", "typicalGrade": None}, "needs its typicalGrade")],
)
def test_a_curriculum_file_must_place_its_course_in_school(tmp_path, change, message):
    import json

    doc = json.loads((DATA / "curriculum" / "tx-math-a1.json").read_text(encoding="utf-8")) | change
    path = tmp_path / "c.json"
    path.write_text(json.dumps(doc), encoding="utf-8")
    with pytest.raises(ValueError, match=message):
        load_curriculum(path)


def test_keys_alternate_choice_letters_by_item():
    for test in RELEASED_TESTS:
        for row in load_key(DATA / "keys" / f"{form_slug(test, test.subject)}.csv"):
            if row.is_choice:
                assert row.answer in ("ABCD" if row.item % 2 else "FGHJ"), row


def test_course_payload_uses_level_code_and_keeps_limits():
    body = course_payload(load_curriculum(DATA / "curriculum" / "tx-math-g3.json"))
    assert (body["jurisdiction"], body["code"]) == ("TX", "G3")
    # The name students see comes from the curriculum file, never from code.
    assert body["jurisdictionName"] == "Texas"
    assert len(body["categories"]) == 4
    fractions = next(s for s in body["skills"] if s["code"] == "3.3(F)")
    assert fractions["limits"]["fractions"]["denominators"] == [2, 3, 4, 6, 8]


def test_extracts_choices_and_griddable_items(tmp_path):
    items = extract_items(_pdf(tmp_path), 2)
    assert items[0].stem == "Which number is greatest?"
    assert [(c.label, c.text) for c in items[0].choices] == [("A", "12"), ("B", "21"), ("C", "9"), ("D", "20")]
    assert items[1].griddable and items[1].stem == "What is 4 x 5?" and items[1].choices == []
    png = render_item(_pdf(tmp_path), items[0], tmp_path / "out" / "01.png")
    assert png.stat().st_size > 0


def test_gridded_items_take_the_kind_the_test_grids(tmp_path):
    items = extract_items(_pdf(tmp_path), 2)
    key = [KeyRow(1, 1, "r", "s", "B"), KeyRow(2, 2, "r", "s", "-38")]
    algebra = ReleasedTest("TX", "EXAM", "A1", 2021, "spring", "https://example.org/a.pdf", tagged=False,
                           gridded_kind="Decimal")
    body = form_payload(algebra, "math", key, items, {1: "a", 2: "b"})
    assert body["items"][1]["answer"] == {"kind": "Decimal", "value": "-38"}
    assert body["items"][0]["answer"] == {"kind": "Choice", "value": "B"}
    # Without a gridded kind the key value decides, as for Grade 3.
    assert form_payload(TEST, "math", key, items, {1: "a", 2: "b"})["items"][1]["answer"]["kind"] == "Integer"


def test_a_whole_number_grid_refuses_a_decimal_key(tmp_path):
    whole = ReleasedTest("TX", "EXAM", "G3", 2021, "spring", "https://example.org/t.pdf", tagged=False,
                         gridded_kind="Integer")
    key = [KeyRow(1, 1, "r", "s", "B"), KeyRow(2, 2, "r", "s", "4.5")]
    with pytest.raises(ValueError, match="not a whole number"):
        form_payload(whole, "math", key, extract_items(_pdf(tmp_path), 2), {1: "a", 2: "b"})


def test_missing_items_are_an_error(tmp_path):
    with pytest.raises(ValueError, match="expected 3"):
        extract_items(_pdf(tmp_path), 3)


def test_form_payload_joins_key_and_items(tmp_path):
    items = extract_items(_pdf(tmp_path), 2)
    key = [KeyRow(1, 1, "readiness", "3.2(D)", "B"), KeyRow(2, 2, "supporting", "3.4(F)", "20")]
    body = form_payload(TEST, "math", key, items, {1: "f/01.png", 2: "f/02.png"})
    first, second = body["items"]
    assert first["answer"] == {"kind": "Choice", "value": "B"} and first["itemType"] == "choice"
    assert second["answer"] == {"kind": "Integer", "value": "20"} and "choices" not in second["content"]
    assert body["courseCode"] == "G3" and body["exam"] == "EXAM"


@pytest.mark.parametrize(
    ("key", "message"),
    [
        ([KeyRow(1, 1, "r", "s", "F"), KeyRow(2, 1, "r", "s", "20")], "not one of the choices"),
        ([KeyRow(1, 1, "r", "s", "B"), KeyRow(2, 1, "r", "s", "G")], "says multiple choice"),
        ([KeyRow(1, 1, "r", "s", "B")], "key has 1 items"),
    ],
)
def test_form_payload_rejects_a_key_that_disagrees_with_the_pdf(tmp_path, key, message):
    with pytest.raises(ValueError, match=message):
        form_payload(TEST, "math", key, extract_items(_pdf(tmp_path), 2), {1: "a", 2: "b"})


def test_publish_released_moves_leftovers_live_and_says_how_many():
    import httpx

    from k12_pipeline.cli import publish_released

    calls = []

    def handler(request: httpx.Request) -> httpx.Response:
        calls.append((request.method, request.url.path))
        return httpx.Response(200, json={"questionsPublished": 64})

    client = httpx.Client(base_url="http://api", transport=httpx.MockTransport(handler))
    assert publish_released(client) == "64 published"
    assert calls == [("POST", "/api/admin/import/publish-released")]
    assert publish_released(None) == "not sent (--dry-run)"
