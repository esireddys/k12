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
    # The answer kind for every gridded item, or None to read it from each key value (Integer or Decimal).
    # Algebra I grids signed decimals, so all its gridded items use "Decimal": the number pad then offers the
    # minus sign and the decimal point on every item and never hints at the answer's form.
    gridded_kind: str | None = None
    # Where the published answer key is, for the record. The key itself is copied into data/keys.
    key_url: str = ""

    @property
    def pdf_name(self) -> str:
        return self.url.rsplit("/", 1)[-1]


TEA_RELEASED = "https://tea.texas.gov/data-reports/staar/released-test-questions/"

# Seed list from the project brief. More years are added as they are found on tea.texas.gov.
# Paper tests stop at 2022: from 2023 TEA publishes released tests online only (answer keys stay PDF).
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
    # Algebra I end-of-course tests (most students take Algebra I in grade 9). 2018 and older are no longer on
    # TEA's released tests page. 54 items each, 5 of them gridded.
    ReleasedTest(
        "TX", "STAAR", "A1", 2022, "spring", TEA_RELEASED + "2022-staar-may-algebra-i-releasedtest.pdf",
        tagged=False, items=54, gridded_kind="Decimal",
        key_url=TEA_RELEASED + "may-2022-staar-algebra-i-answerkey-final-2.pdf",
    ),
    ReleasedTest(
        "TX", "STAAR", "A1", 2021, "spring", TEA_RELEASED + "2021-staar-algebra-i-test-tagged.pdf",
        tagged=True, items=54, gridded_kind="Decimal",
        key_url=TEA_RELEASED + "staar-2021-key-eoc-alg1-math-tagged.pdf",
    ),
    ReleasedTest(
        "TX", "STAAR", "A1", 2019, "spring", TEA_RELEASED + "2019-staar-algebra-i-test-tagged.pdf",
        tagged=True, items=54, gridded_kind="Decimal",
        key_url=TEA_RELEASED + "staar-2019-key-eoc-alg1-tagged.pdf",
    ),
]
