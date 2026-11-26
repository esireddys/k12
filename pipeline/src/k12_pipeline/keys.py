"""Answer keys as published with each released test, stored as CSV in data/keys."""

from __future__ import annotations

import csv
from dataclasses import dataclass
from pathlib import Path


@dataclass(frozen=True)
class KeyRow:
    item: int
    category: int
    designation: str
    skill: str
    answer: str

    @property
    def is_choice(self) -> bool:
        return self.answer.isalpha()


def load_key(path: Path) -> list[KeyRow]:
    with path.open(newline="", encoding="utf-8") as f:
        rows = [
            KeyRow(int(r["item"]), int(r["reporting_category"]), r["readiness_or_supporting"].strip().lower(),
                   r["teks"].strip(), r["correct_answer"].strip().upper())
            for r in csv.DictReader(f)
        ]
    numbers = [r.item for r in rows]
    if numbers != list(range(1, len(rows) + 1)):
        raise ValueError(f"{path.name}: items must run 1..{len(rows)} in order")
    return rows
