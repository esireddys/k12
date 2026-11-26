"""The reviewer's instructions. Exam, framework and course names come from the question's source data."""

from __future__ import annotations

import base64
import json
from typing import Any

SYSTEM = """You review math questions taken from released state tests before they are shown to children.
An app owner makes the final decision; your job is to give them an accurate, careful first pass.

You get the picture of the printed item (the source of truth), the text a PDF extractor read from it
(often damaged: stacked fractions flattened, minus and division signs lost as �, figure labels
scattered), the published answer key, and the skill the item is tagged to.

Do this for every item:
1. Read the picture carefully and solve the item yourself before you look at the key. Report your answer.
2. Compare with the key. If they differ, explain in a problem finding which one you believe and why.
3. Rewrite the question and every choice exactly as printed, fixing extraction damage. Keep the wording;
   do not simplify or reword. Write fractions as 3/4, mixed numbers as 1 3/4, money as $4.50,
   multiplication as ×, division as ÷. Leave out the item number and answer-sheet instructions.
4. Say whether a student needs the picture to answer, and describe any figure in one or two sentences.
5. Write a short explanation for a student of this grade: the steps, in plain words, ending with the answer.
6. Judge difficulty against typical items for the same skill at this grade.
7. Record findings for anything an owner should know: a key you disagree with, text you could not read,
   a figure cut off at the edge of the picture, a skill tag that does not fit. Do not add findings
   just to say things are fine.

Be exact. If part of the picture is unreadable, say so in a finding instead of guessing."""


def media_type(path: str) -> str:
    lower = path.lower()
    if lower.endswith((".jpg", ".jpeg")):
        return "image/jpeg"
    if lower.endswith(".webp"):
        return "image/webp"
    return "image/png"


def describe_item(item: dict[str, Any]) -> str:
    """The text part of the request: source, skill, answer key and the extracted text."""
    source = item.get("source") or {}
    content = item.get("content") or {}
    answer = item.get("answer") or {}
    skills = item.get("skills") or []
    lines = []
    if source:
        lines.append(
            f"Source: {source.get('jurisdiction')} {source.get('exam')} {source.get('season')} {source.get('year')}, "
            f"{source.get('courseName')} {source.get('subject')}, item {source.get('itemNumber')}."
        )
        lines.append(f"Skill framework: {source.get('frameworkName')}.")
    for skill in skills:
        limits = skill.get("limits") or {}
        rule = f" Limits: {json.dumps(limits)}." if limits else ""
        lines.append(f"Tagged skill {skill.get('code')}: {skill.get('description')}.{rule}")
    kind = answer.get("kind")
    if item.get("itemType") == "choice":
        lines.append(f"Item type: multiple choice. Published key: {answer.get('value')}.")
    else:
        lines.append(f"Item type: typed answer ({kind}). Published key: {answer.get('value')}.")
    lines.append("Extracted text (may be damaged):")
    lines.append(f"Question: {content.get('stem', '')}")
    for choice in content.get("choices") or []:
        lines.append(f"Choice {choice.get('label')}: {choice.get('text')}")
    return "\n".join(lines)


def user_content(item: dict[str, Any], image: bytes, image_path: str) -> list[dict[str, Any]]:
    return [
        {
            "type": "image",
            "source": {
                "type": "base64",
                "media_type": media_type(image_path),
                "data": base64.standard_b64encode(image).decode("ascii"),
            },
        },
        {"type": "text", "text": describe_item(item)},
    ]
