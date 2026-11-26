"""Where released tests come from. Add a row per released form; the importer reads this list."""

from dataclasses import dataclass


@dataclass(frozen=True)
class ReleasedTest:
    jurisdiction: str
    exam: str
    level: str
    year: int
    season: str
    url: str
    tagged: bool
    subject: str = "math"
    items: int = 32

    @property
    def pdf_name(self) -> str:
        return self.url.rsplit("/", 1)[-1]


# Seed list from the project brief. More years are added as they are found on tea.texas.gov.
RELEASED_TESTS: list[ReleasedTest] = [
    ReleasedTest(
        "TX", "STAAR", "G3", 2022, "spring",
        "https://tea.texas.gov/data-reports/staar/released-test-questions/2022-staar-may-grade-3-math-releasedtest.pdf",
        tagged=False,
    ),
    ReleasedTest(
        "TX", "STAAR", "G3", 2021, "spring",
        "https://tea.texas.gov/data-reports/staar/released-test-questions/2021-staar-gr3-mathematics-test-tagged.pdf",
        tagged=True,
    ),
]
