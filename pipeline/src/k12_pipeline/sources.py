"""Where released tests come from. Add a row per released form; the importer reads this list."""

from dataclasses import dataclass
from pathlib import Path


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
    # Reading tests: each selection becomes a passage shown beside its items.
    has_passages: bool = False
    # The language the test is in. Every test is English for now; a Spanish form would be its own course.
    language: str = "en"
    # Other file names the PDF is known to download as (the site sometimes serves a renamed copy).
    also_named: tuple[str, ...] = ()

    @property
    def pdf_name(self) -> str:
        return self.url.rsplit("/", 1)[-1]

    def find_pdf(self, raw: Path) -> Path | None:
        """The downloaded PDF: in raw/<state>/<subject>/ (the layout to use), else straight in raw/ (older layout)."""
        folders = [raw / self.jurisdiction.lower() / self.subject, raw]
        names = [self.pdf_name, *self.also_named]
        return next((f / n for f in folders for n in names if (f / n).exists()), None)

    def expected_path(self, raw: Path) -> Path:
        return raw / self.jurisdiction.lower() / self.subject / self.pdf_name


TEA_RELEASED = "https://tea.texas.gov/data-reports/staar/released-test-questions/"

# Seed list from the project brief. More years are added as they are found on tea.texas.gov.
# Paper tests stop at 2022: from 2023 TEA publishes released tests online only (answer keys stay PDF).
TEA_RELEASED_OLD = "https://tea.texas.gov/student-assessment/staar/released-test-questions/"

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
    ReleasedTest(
        "TX", "STAAR", "G3", 2019, "spring", TEA_RELEASED_OLD + "2019-staar-3-math-test.pdf",
        tagged=False, key_url=TEA_RELEASED_OLD + "2019-staar-3-math-key.pdf",
        also_named=("2019-staar-gr3-mathematics-test-2.pdf", "2019-staar-gr3-mathematics-test.pdf"),
    ),
    # Grade 3 reading: 34 multiple-choice items about five selections. 2019 and older tested the earlier reading
    # standards, whose codes mean different things (old 3.8(A) is not today's 3.8(A)), so they are left out.
    ReleasedTest(
        "TX", "STAAR", "G3", 2022, "spring", TEA_RELEASED + "2022-staar-may-grade-3-reading-releasedtest.pdf",
        tagged=False, subject="reading", items=34, has_passages=True,
        key_url=TEA_RELEASED + "may-2022-staar-reading-3-answerkey-final-2.pdf",
    ),
    ReleasedTest(
        "TX", "STAAR", "G3", 2021, "spring", TEA_RELEASED + "2021-staar-gr3-reading-test.pdf",
        tagged=False, subject="reading", items=34, has_passages=True,
        key_url=TEA_RELEASED + "staar-2021-key-g3-reading-3.pdf",
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
