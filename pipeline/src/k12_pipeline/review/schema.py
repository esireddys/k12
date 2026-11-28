"""What the AI reviewer must return. Validated by the SDK (structured outputs) before any of it is used."""

from __future__ import annotations

from typing import Literal

from pydantic import BaseModel, Field

Severity = Literal["info", "warning", "problem"]
Area = Literal["answer_key", "stem", "choices", "figure", "skill", "difficulty", "other"]
DifficultyName = Literal["Easier", "OnLevel", "Harder"]


class Finding(BaseModel):
    severity: Severity = Field(description="problem: an owner must fix or reject; warning: check it; info: FYI")
    area: Area
    message: str = Field(description="One or two plain sentences an owner can act on")


class CleanChoice(BaseModel):
    label: str = Field(description="The choice letter exactly as printed, for example F")
    text: str = Field(description="The choice as printed, cleaned up")


class ReviewOutput(BaseModel):
    solved_answer: str = Field(
        description="The answer you get by solving the item from the picture: a choice letter for multiple "
        "choice, otherwise the number only (no units)"
    )
    answer_key_agrees: bool = Field(description="Whether your solved answer matches the published key")
    stem: str = Field(description="The question text as printed, cleaned up. No item number.")
    choices: list[CleanChoice] = Field(description="Every printed choice in order; empty for typed-answer items")
    needs_picture: bool = Field(
        description="True when a student cannot answer from the text alone (figures, tables, graphs, "
        "number lines, stacked fractions, models)"
    )
    figure_description: str = Field(
        description="A short description of any figure for a student who cannot see it; empty when there is none"
    )
    explanation: str = Field(
        description="A short worked explanation for a student of this grade, shown after they answer in practice"
    )
    suggested_difficulty: DifficultyName = Field(
        description="Easier, OnLevel or Harder, relative to typical items for this skill at this grade"
    )
    skill_fits: bool = Field(description="Whether the item really assesses the tagged skill")
    findings: list[Finding]
    summary: str = Field(description="One sentence for the owner's queue")
