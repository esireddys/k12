"""Curriculum seed files in data/curriculum, and the course import payload the API takes."""

from __future__ import annotations

import json
from pathlib import Path
from typing import Any


def load_curriculum(path: Path) -> dict[str, Any]:
    doc = json.loads(path.read_text(encoding="utf-8"))
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
