"""The AI reviewer's code-side checks and the review run, with a fake model and a fake API (no network)."""

from __future__ import annotations

import json
from pathlib import Path
from types import SimpleNamespace
from typing import Any

import httpx
import pytest

from k12_pipeline.review.prompt import SYSTEM, describe_item, user_content
from k12_pipeline.review.reviewer import (
    AiReviewer,
    ModelReview,
    ReviewerUnavailable,
    build_payload,
    key_matches,
    unreviewable_payload,
)
from k12_pipeline.review.runner import run_review
from k12_pipeline.review.schema import CleanChoice, Finding, ReviewOutput


def item(item_type: str = "choice", key: str = "B", **content: Any) -> dict[str, Any]:
    base: dict[str, Any] = {"image": "form/01.png", "stem": "Wh�ch is 3/4?", "textNeedsReview": True}
    if item_type == "choice":
        base["choices"] = [{"label": label, "text": t} for label, t in zip("ABCD", ["1", "2", "3", "4"], strict=True)]
    base.update(content)
    return {
        "id": "11111111-1111-1111-1111-111111111111",
        "version": 1,
        "itemType": item_type,
        "content": base,
        "answer": {"kind": "Choice" if item_type == "choice" else "Integer", "value": key},
        "difficulty": "OnLevel",
        "skills": [{"code": "3.3(F)", "description": "Fractions on a number line", "limits": {"maxDenominator": 8}}],
        "source": {"jurisdiction": "TX", "exam": "EXAMX", "season": "spring", "year": 2022, "courseName": "Grade 3",
                   "subject": "math", "itemNumber": 7, "frameworkName": "FrameworkY"},
    }


def output(solved: str = "B", agrees: bool = True, labels: str = "ABCD", **kw: Any) -> ReviewOutput:
    fields: dict[str, Any] = {
        "solved_answer": solved, "answer_key_agrees": agrees, "stem": "Which is 3/4?",
        "choices": [CleanChoice(label=label, text=f"choice {label}") for label in labels],
        "needs_picture": True, "figure_description": "A number line from 0 to 1.",
        "explanation": "Count the parts.", "suggested_difficulty": "Easier", "skill_fits": True,
        "findings": [], "summary": "Clean.",
    }
    fields.update(kw)
    return ReviewOutput(**fields)


@pytest.mark.parametrize(
    ("item_type", "key", "solved", "expected"),
    [
        ("choice", "B", "B", True),
        ("choice", "B", "b", True),
        ("choice", "B", "B)", True),
        ("choice", "B", "C", False),
        ("numeric", "24", "24", True),
        ("numeric", "24", "24.0", True),
        ("numeric", "4.5", "$4.50", True),
        ("numeric", "1200", "1,200", True),
        ("numeric", "24", "25", False),
        ("numeric", "24", "twenty-four", False),
    ],
)
def test_key_matching(item_type: str, key: str, solved: str, expected: bool) -> None:
    assert key_matches(item(item_type, key), solved) is expected


def test_a_clean_review_looks_good_and_proposes_cleaned_content() -> None:
    payload = build_payload(item(), ModelReview(output(), "claude-opus-5-5"))

    assert payload["verdict"] == "LooksGood"
    assert payload["answerKeyAgrees"] is True
    assert payload["findings"] == []
    proposed = payload["proposedContent"]
    assert proposed["stem"] == "Which is 3/4?"
    assert proposed["image"] == "form/01.png"
    assert proposed["textNeedsReview"] is False
    assert proposed["showImage"] is True
    assert [c["label"] for c in proposed["choices"]] == list("ABCD")
    assert payload["reviewer"].startswith("claude-opus-5-5")


@pytest.mark.parametrize(
    ("review", "area"),
    [
        (output(solved="C", agrees=False), "answer_key"),
        (output(solved="C", agrees=True), "answer_key"),  # the model's claim and its own answer disagree
        (output(labels="ABC"), "choices"),
        (output(skill_fits=False), "skill"),
        (output(findings=[Finding(severity="problem", area="figure", message="Cut off.")]), "figure"),
    ],
)
def test_reviews_with_a_problem_need_attention(review: ReviewOutput, area: str) -> None:
    payload = build_payload(item(), ModelReview(review, "m"))
    assert payload["verdict"] == "NeedsAttention"
    assert area in {f["area"] for f in payload["findings"]}
    assert {f["severity"] for f in payload["findings"]} <= {"Problem", "Warning", "Info"}


def test_mismatched_choice_letters_keep_the_extracted_choices() -> None:
    payload = build_payload(item(), ModelReview(output(labels="ABC"), "m"))
    assert [c["text"] for c in payload["proposedContent"]["choices"]] == ["1", "2", "3", "4"]


def test_leftover_unreadable_characters_are_flagged() -> None:
    payload = build_payload(item(), ModelReview(output(stem="Wh�ch"), "m"))
    assert any(f["area"] == "stem" and f["severity"] == "Warning" for f in payload["findings"])


def test_typed_answer_items_have_no_choices() -> None:
    payload = build_payload(item("numeric", "24"), ModelReview(output(solved="24", labels=""), "m"))
    assert payload["verdict"] == "LooksGood"
    assert "choices" not in payload["proposedContent"]


def test_an_unreviewable_item_still_reaches_an_owner_flagged_and_unchanged() -> None:
    it = item()
    payload = unreviewable_payload(it, "the model declined", "m")
    assert payload["verdict"] == "NeedsAttention"
    assert payload["proposedContent"] == it["content"]
    assert payload["findings"][0]["severity"] == "Problem"


def test_the_prompt_uses_source_data_and_never_assumes_an_exam_name() -> None:
    text = describe_item(item())
    assert "EXAMX" in text and "FrameworkY" in text and "Published key: B" in text and "maxDenominator" in text
    for name in ("STAAR", "TEKS", "Texas"):
        assert name not in SYSTEM
    blocks = user_content(item(), b"\x89PNG", "form/01.png")
    assert blocks[0]["type"] == "image" and blocks[0]["source"]["media_type"] == "image/png"


class FakeMessages:
    def __init__(self, response: Any) -> None:
        self.response = response
        self.calls: list[dict[str, Any]] = []

    def parse(self, **kwargs: Any) -> Any:
        self.calls.append(kwargs)
        return self.response


def fake_client(response: Any) -> SimpleNamespace:
    return SimpleNamespace(beta=SimpleNamespace(messages=FakeMessages(response)))


def test_the_reviewer_asks_for_structured_output_with_fallbacks() -> None:
    client = fake_client(SimpleNamespace(stop_reason="end_turn", parsed_output=output(), model="claude-opus-5-5"))
    result = AiReviewer(client).review(item(), b"png", "form/01.png")

    call = client.beta.messages.calls[0]
    assert call["model"] == "claude-opus-5-5"
    assert call["output_format"] is ReviewOutput
    assert call["fallbacks"] == "default"
    assert "thinking" not in call and "temperature" not in call
    assert result.output.solved_answer == "B"


@pytest.mark.parametrize("stop_reason", ["refusal", "max_tokens"])
def test_the_reviewer_reports_when_it_cannot_finish(stop_reason: str) -> None:
    client = fake_client(SimpleNamespace(stop_reason=stop_reason, stop_details=None, parsed_output=None, model="m"))
    with pytest.raises(ReviewerUnavailable):
        AiReviewer(client).review(item(), b"png", "form/01.png")


def test_a_review_run_sends_one_review_per_item(tmp_path: Path) -> None:
    (tmp_path / "form").mkdir()
    (tmp_path / "form" / "01.png").write_bytes(b"png")
    missing = item() | {"id": "22222222-2222-2222-2222-222222222222", "content": {"image": "form/99.png", "stem": "?"}}
    sent: dict[str, Any] = {}

    def handler(request: httpx.Request) -> httpx.Response:
        if request.method == "GET":
            return httpx.Response(200, json={"items": [item(), missing]})
        sent[request.url.path.split("/")[-2]] = json.loads(request.content)
        return httpx.Response(200, json={"status": "AwaitingOwner"})

    http = httpx.Client(base_url="http://api", transport=httpx.MockTransport(handler))
    reviewer = AiReviewer(fake_client(SimpleNamespace(stop_reason="end_turn", parsed_output=output(), model="m")))

    stats = run_review("http://api", tmp_path, 10, dry_run=False, reviewer=reviewer, http=http)

    assert stats == {"reviewed": 2, "flagged": 1, "skipped": 0}
    assert sent["11111111-1111-1111-1111-111111111111"]["verdict"] == "LooksGood"
    assert sent["22222222-2222-2222-2222-222222222222"]["verdict"] == "NeedsAttention"
    assert (tmp_path / "reviews" / "11111111-1111-1111-1111-111111111111.json").exists()
