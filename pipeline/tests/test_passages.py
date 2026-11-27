from pathlib import Path

import pymupdf
import pytest

from k12_pipeline.curriculum import course_payload, load_curriculum
from k12_pipeline.extract import extract_items
from k12_pipeline.forms import form_payload
from k12_pipeline.keys import KeyRow
from k12_pipeline.passages import find_passages, render_passage_pages
from k12_pipeline.sources import RELEASED_TESTS, ReleasedTest

DATA = Path(__file__).resolve().parents[1] / "data"
READING = ReleasedTest("TX", "EXAM", "G3", 2021, "spring", "https://example.org/r.pdf", tagged=False,
                       subject="reading", items=3, has_passages=True)


def _reading_pdf(tmp_path: Path) -> Path:
    """Two selections laid out like a released reading test: instruction, big bold title, numbered paragraphs,
    then a page of items. Paragraph numbers are plain; item numbers are bold."""
    doc = pymupdf.open()

    def page(lines):
        p = doc.new_page(width=612, height=792)
        for x, y, text, bold, size in lines:
            p.insert_text((x, y), text, fontname="hebo" if bold else "helv", fontsize=size)
        p.insert_text((36, 760), "Reading", fontname="helv", fontsize=10)

    page([(90, 60, "Read the selection and choose the best answer to each question.", True, 12),
          (150, 100, "The Fox and the Crow", True, 22),
          (99, 140, "1 A crow sat in a tree.", False, 12), (119, 156, "She held some cheese.", False, 12)])
    page([(99, 60, "2 A fox walked by.", False, 12)])
    page([(60, 100, "1 What did the crow hold?", True, 12),
          (75, 130, "A Bread", True, 12), (75, 150, "B Cheese", True, 12), (75, 170, "C A leaf", True, 12),
          (75, 190, "D A stick", True, 12),
          (60, 300, "2 Who walked by?", True, 12),
          (75, 330, "F A dog", True, 12), (75, 350, "G A cat", True, 12), (75, 370, "H A fox", True, 12),
          (75, 390, "J A crow", True, 12)])
    page([(90, 60, "Read the selection and choose the best answer to each question.", True, 12),
          (150, 100, "Rain", True, 22), (99, 140, "1 Rain falls.", False, 12)])
    page([(60, 100, "3 What falls?", True, 12),
          (75, 130, "A Rain", True, 12), (75, 150, "B Snow", True, 12), (75, 170, "C Hail", True, 12),
          (75, 190, "D Leaves", True, 12)])
    path = tmp_path / "r.pdf"
    doc.save(path)
    return path


def test_finds_each_selection_its_pages_and_its_items(tmp_path):
    pdf = _reading_pdf(tmp_path)
    items = extract_items(pdf, 3)
    passages = find_passages(pdf, items)

    assert [(p.key, p.title, p.pages, p.items) for p in passages] == [
        ("p1", "The Fox and the Crow", [1, 2], [1, 2]),
        ("p2", "Rain", [4], [3]),
    ]
    lines = passages[0].text.splitlines()
    assert "1 A crow sat in a tree. She held some cheese." in lines and "2 A fox walked by." in lines
    assert "Read the selection" not in passages[0].text


def test_renders_one_picture_per_page(tmp_path):
    pdf = _reading_pdf(tmp_path)
    passage = find_passages(pdf, extract_items(pdf, 3))[0]
    paths = render_passage_pages(pdf, passage, tmp_path / "out")
    assert [p.name for p in paths] == ["p1-1.png", "p1-2.png"]
    assert all(p.stat().st_size > 0 for p in paths)


def test_form_payload_links_items_to_passages_and_keeps_item_categories(tmp_path):
    pdf = _reading_pdf(tmp_path)
    items = extract_items(pdf, 3)
    passages = find_passages(pdf, items)
    key = [KeyRow(1, 2, "readiness", "3.6(F)", "B"), KeyRow(2, 2, "readiness", "3.8(C)", "H"),
           KeyRow(3, 3, "readiness", "3.6(F)", "A")]
    payload = form_payload(READING, "reading", key, items, {n: f"r/{n:02}.png" for n in (1, 2, 3)},
                           passages, {"p1": ["r/p1-1.png", "r/p1-2.png"], "p2": ["r/p2-1.png"]})

    assert [(i["number"], i["passage"], i["category"]) for i in payload["items"]] == [(1, "p1", 2), (2, "p1", 2), (3, "p2", 3)]
    assert payload["language"] == "en"
    first = payload["passages"][0]
    assert first["key"] == "p1" and first["title"] == "The Fox and the Crow"
    assert first["content"]["pages"] == ["r/p1-1.png", "r/p1-2.png"]
    assert first["content"]["textNeedsReview"] is True


def test_a_reading_test_refuses_items_before_any_selection(tmp_path):
    pdf = _reading_pdf(tmp_path)
    items = extract_items(pdf, 3)
    passages = find_passages(pdf, items)[1:]  # pretend the first selection was not found
    key = [KeyRow(n, 2, "readiness", "3.6(F)", a) for n, a in ((1, "B"), (2, "H"), (3, "A"))]
    with pytest.raises(ValueError, match="no selection"):
        form_payload(READING, "reading", key, items, {n: "x.png" for n in (1, 2, 3)}, passages, {})


def test_pdf_is_found_in_the_state_and_subject_folder_or_the_old_flat_folder(tmp_path):
    raw = tmp_path / "raw"
    (raw / "tx" / "reading").mkdir(parents=True)
    assert READING.find_pdf(raw) is None
    (raw / "r.pdf").write_bytes(b"%PDF")
    assert READING.find_pdf(raw) == raw / "r.pdf"
    (raw / "tx" / "reading" / "r.pdf").write_bytes(b"%PDF")
    assert READING.find_pdf(raw) == raw / "tx" / "reading" / "r.pdf"
    assert READING.expected_path(raw) == raw / "tx" / "reading" / "r.pdf"


def test_grade_3_has_math_and_reading_with_a_full_practice_test_each():
    docs = {p.stem: load_curriculum(p) for p in (DATA / "curriculum").glob("tx-*-g3.json")}
    assert set(docs) == {"tx-math-g3", "tx-reading-g3"}
    for doc in docs.values():
        test = course_payload(doc)["practiceTest"]
        assert test["minutes"] > 0 and sum(m["items"] for m in test["mix"]) == test["items"]
    reading_years = sorted(t.year for t in RELEASED_TESTS if t.subject == "reading" and t.level == "G3")
    assert reading_years == [2021, 2022]
    assert all(t.has_passages for t in RELEASED_TESTS if t.subject == "reading")


def test_a_skill_without_a_category_is_allowed_but_an_unknown_category_is_not(tmp_path):
    doc = (DATA / "curriculum" / "tx-reading-g3.json").read_text(encoding="utf-8")
    assert course_payload(load_curriculum(DATA / "curriculum" / "tx-reading-g3.json"))["skills"][3]["category"] is None
    bad = tmp_path / "bad.json"
    bad.write_text(doc.replace('"category": 2,\n      "designation"', '"category": 9,\n      "designation"', 1), encoding="utf-8")
    with pytest.raises(ValueError, match="unknown categories"):
        load_curriculum(bad)
