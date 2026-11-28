"""Curriculum seed files in data/curriculum, and the course import payload the API takes."""

from __future__ import annotations

import json
from pathlib import Path
from typing import Any

COURSE_KINDS = {"grade", "course"}


def load_curriculum(path: Path) -> dict[str, Any]:
    doc = json.loads(path.read_text(encoding="utf-8"))
    kind = doc.get("kind", "grade")
    if kind not in COURSE_KINDS:
        raise ValueError(f"{path.name}: kind is grade or course, not {kind}")
    grade = doc.get("typicalGrade")
    if grade is not None and not (isinstance(grade, int) and 0 <= grade <= 12):
        raise ValueError(f"{path.name}: typicalGrade runs from 0 (kindergarten) to 12")
    if kind == "grade" and grade is None:
        raise ValueError(f"{path.name}: a grade needs its typicalGrade")
    categories = {c["number"] for c in doc["categories"]}
    codes = [s["code"] for s in doc["skills"]]
    if len(codes) != len(set(codes)):
        raise ValueError(f"{path.name}: duplicate skill codes")
    # A skill reported in more than one category (reading skills count as literary or informational depending on
    # the selection) has no category of its own: null. Its items carry their category from the answer key.
    stray = [s["code"] for s in doc["skills"] if s.get("category") is not None and s["category"] not in categories]
    if stray:
        raise ValueError(f"{path.name}: skills in unknown categories: {stray}")
    test = doc.get("practiceTest")
    if test is not None:
        mix = test.get("mix", [])
        if any(m["category"] not in categories for m in mix):
            raise ValueError(f"{path.name}: practiceTest names an unknown category")
        if sum(m["items"] for m in mix) > test["items"]:
            raise ValueError(f"{path.name}: practiceTest mix adds up to more than its items")
    return doc


def course_payload(doc: dict[str, Any]) -> dict[str, Any]:
    """The body for POST /api/admin/import/course."""
    return {
        "jurisdiction": doc["jurisdiction"],
        "jurisdictionName": doc.get("jurisdictionName", ""),
        "subject": doc["subject"],
        "code": doc["level"],
        "name": doc["levelName"],
        "frameworkCode": doc["framework"]["code"],
        "frameworkName": doc["framework"]["name"],
        # A whole grade (Grade 3 math) or a course taken in some grade (Algebra I, usually grade 9).
        "kind": doc.get("kind", "grade"),
        "typicalGrade": doc.get("typicalGrade"),
        "language": doc.get("language", "en"),
        # The real statewide test's shape (items, minutes, items per category), for the full practice test.
        "practiceTest": doc.get("practiceTest"),
        "categories": doc["categories"],
        "skills": [
            {
                "code": s["code"],
                "description": s["description"],
                "category": s.get("category"),
                "designation": s.get("designation"),
                "limits": s.get("limits", {}),
            }
            for s in doc["skills"]
        ],
    }
