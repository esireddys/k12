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
    stray = [s["code"] for s in doc["skills"] if s["category"] not in categories]
    if stray:
        raise ValueError(f"{path.name}: skills in unknown categories: {stray}")
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
        "categories": doc["categories"],
        "skills": [
            {
                "code": s["code"],
                "description": s["description"],
                "category": s["category"],
                "designation": s.get("designation"),
                "limits": s.get("limits", {}),
            }
            for s in doc["skills"]
        ],
    }
