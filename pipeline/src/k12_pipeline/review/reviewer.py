"""Calls Claude for one question and turns its answer into the API's AI review body.

The model does the reading and solving; plain code double-checks what can be checked without it
(choice letters, the key comparison, leftover unreadable characters) and decides the verdict.
"""

from __future__ import annotations

import os
from dataclasses import dataclass
from decimal import Decimal, InvalidOperation
from typing import Any, Protocol

from . import prompt
from .schema import ReviewOutput

DEFAULT_MODEL = os.environ.get("K12_REVIEW_MODEL", "claude-opus-5-5")
PROMPT_VERSION = "reviewer-v1"
UNREADABLE = "�"


class ReviewerUnavailable(Exception):
    """The model could not give a review (refused, cut off, or returned nothing usable)."""


class _Client(Protocol):  # the slice of anthropic.Anthropic this module uses, so tests can pass a fake
    beta: Any


@dataclass
class ModelReview:
    output: ReviewOutput
    model: str


class AiReviewer:
    def __init__(self, client: _Client, model: str = DEFAULT_MODEL, effort: str = "high") -> None:
        self._client = client
        self._model = model
        self._effort = effort

    def review(self, item: dict[str, Any], image: bytes, image_path: str) -> ModelReview:
        response = self._client.beta.messages.parse(
            model=self._model,
            max_tokens=16000,
            system=prompt.SYSTEM,
            messages=[{"role": "user", "content": prompt.user_content(item, image, image_path)}],
            output_format=ReviewOutput,
            output_config={"effort": self._effort},
            # If the model declines for policy reasons, the API retries on its default fallback model.
            betas=["server-side-fallback-2026-07-01"],
            fallbacks="default",
        )
        if response.stop_reason == "refusal":
            category = getattr(response.stop_details, "category", None)
            raise ReviewerUnavailable(f"the model declined (category: {category})")
        if response.stop_reason == "max_tokens":
            raise ReviewerUnavailable("the review ran out of room before it finished")
        parsed = response.parsed_output
        if parsed is None:
            raise ReviewerUnavailable("the model returned no review")
        return ModelReview(parsed, response.model)


def _number(text: str) -> Decimal | None:
    cleaned = text.strip().replace("$", "").replace(",", "").replace(" ", "")
    try:
        return Decimal(cleaned)
    except InvalidOperation:
        return None


def key_matches(item: dict[str, Any], solved: str) -> bool:
    key = str((item.get("answer") or {}).get("value", "")).strip()
    solved = solved.strip()
    if item.get("itemType") == "choice":
        return solved.upper().rstrip(".)") == key.upper()
    a, b = _number(solved), _number(key)
    return a is not None and a == b


def build_payload(item: dict[str, Any], review: ModelReview) -> dict[str, Any]:
    """The body for POST /api/admin/review/questions/{id}/ai-review."""
    out = review.output
    findings = [f.model_dump() for f in out.findings]

    def add(severity: str, area: str, message: str) -> None:
        findings.append({"severity": severity, "area": area, "message": message})

    agrees = key_matches(item, out.solved_answer)
    if agrees != out.answer_key_agrees:
        add("warning", "answer_key",
            f"The reviewer said the key {'agrees' if out.answer_key_agrees else 'disagrees'}, but its answer "
            f"{out.solved_answer!r} and the key {item['answer']['value']!r} "
            f"{'match' if agrees else 'do not match'}. Solve it yourself.")
    if not agrees and not any(f["severity"] == "problem" and f["area"] == "answer_key" for f in findings):
        add("problem", "answer_key",
            f"The reviewer got {out.solved_answer!r}; the published key is {item['answer']['value']!r}.")

    content = item.get("content") or {}
    original_labels = [c.get("label") for c in content.get("choices") or []]
    cleaned_labels = [c.label for c in out.choices]
    choices = [c.model_dump() for c in out.choices]
    if item.get("itemType") == "choice" and cleaned_labels != original_labels:
        add("problem", "choices",
            f"The reviewer returned choices {cleaned_labels} but the item has {original_labels}; "
            "the extracted choices are kept.")
        choices = content.get("choices") or []

    cleaned_text = " ".join([out.stem, *(c["text"] for c in choices)])
    if UNREADABLE in cleaned_text:
        add("warning", "stem", "Some characters are still unreadable in the cleaned text. Check against the picture.")
    if not out.skill_fits and not any(f["area"] == "skill" for f in findings):
        add("warning", "skill", "The reviewer thinks the item does not fit its skill tag.")

    has_problem = any(f["severity"] == "problem" for f in findings)
    verdict = "LooksGood" if agrees and not has_problem and out.skill_fits else "NeedsAttention"

    proposed = {k: v for k, v in content.items() if k not in {"stem", "choices", "textNeedsReview"}}
    proposed.update({
        "stem": out.stem.strip(),
        "showImage": out.needs_picture,
        "textNeedsReview": False,
    })
    if choices:
        proposed["choices"] = choices
    if out.figure_description.strip():
        proposed["figureDescription"] = out.figure_description.strip()
    if out.explanation.strip():
        proposed["explanation"] = out.explanation.strip()

    return {
        "version": item["version"],
        "reviewer": f"{review.model} ({PROMPT_VERSION})",
        "verdict": verdict,
        "answerKeyAgrees": agrees,
        "solvedAnswer": out.solved_answer.strip(),
        "suggestedDifficulty": out.suggested_difficulty,
        "proposedContent": proposed,
        "summary": out.summary.strip() or "Reviewed against the picture.",
        "findings": [{"severity": f["severity"].capitalize(), "area": f["area"], "message": f["message"]}
                     for f in findings],
    }


def unreviewable_payload(item: dict[str, Any], reason: str, reviewer: str) -> dict[str, Any]:
    """
    When the reviewer cannot judge an item at all (no picture, the model declined), the item still goes to an
    owner, flagged, with its content unchanged, so nothing is stuck in AI review for ever.
    """
    content = dict(item.get("content") or {})
    return {
        "version": item["version"],
        "reviewer": f"{reviewer} ({PROMPT_VERSION})",
        "verdict": "NeedsAttention",
        "answerKeyAgrees": False,
        "solvedAnswer": "",
        "suggestedDifficulty": item.get("difficulty") or "OnLevel",
        "proposedContent": content,
        "summary": f"The AI reviewer could not review this item: {reason}.",
        "findings": [{"severity": "Problem", "area": "other",
                      "message": f"Not reviewed: {reason}. Check the text and the key against the picture yourself."}],
    }
