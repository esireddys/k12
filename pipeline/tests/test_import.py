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


def test_every_key_file_matches_the_curriculum():
    skills = {s["code"]: s for s in load_curriculum(DATA / "curriculum" / "tx-math-g3.json")["skills"]}
    for test in RELEASED_TESTS:
        for row in load_key(DATA / "keys" / f"{form_slug(test, test.subject)}.csv"):
            assert row.skill in skills, row
            assert skills[row.skill]["category"] == row.category, row
            assert skills[row.skill]["designation"] == row.designation, row


def test_keys_alternate_choice_letters_by_item():
    for test in RELEASED_TESTS:
        for row in load_key(DATA / "keys" / f"{form_slug(test, test.subject)}.csv"):
            if row.is_choice:
                assert row.answer in ("ABCD" if row.item % 2 else "FGHJ"), row


def test_course_payload_uses_level_code_and_keeps_limits():
    body = course_payload(load_curriculum(DATA / "curriculum" / "tx-math-g3.json"))
    assert (body["jurisdiction"], body["code"]) == ("TX", "G3")
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
