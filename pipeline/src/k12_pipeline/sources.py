"""Where released tests come from. Add a row per released form; the importer reads this list."""

from dataclasses import dataclass
from pathlib import Path
from urllib.parse import unquote


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
    # TEA's own link when `url` is an archived copy (TEA took 2018 and older tests off its site). The import
    # records this one as the source.
    archived_from: str = ""

    @property
    def source_url(self) -> str:
        return self.archived_from or self.url

    @property
    def pdf_name(self) -> str:
        # Browsers save "Algebra%20I" as "Algebra I".
        return unquote(self.url.rsplit("/", 1)[-1])

    def find_pdf(self, raw: Path) -> Path | None:
        """The downloaded PDF: in raw/<state>/<subject>/ (the layout to use), else straight in raw/ (older layout)."""
        folders = [raw / self.jurisdiction.lower() / self.subject, raw]
        names = [self.pdf_name, *self.also_named]
        return next((f / n for f in folders for n in names if (f / n).exists()), None)

    def expected_path(self, raw: Path) -> Path:
        return raw / self.jurisdiction.lower() / self.subject / self.pdf_name


TEA_RELEASED = "https://tea.texas.gov/data-reports/staar/released-test-questions/"
TEA_RELEASED_PAGE = "https://tea.texas.gov/data-reports/staar/staar-released-test-questions"

# Paper tests stop at 2022: from 2023 TEA publishes released tests online only (answer keys stay PDF).
TEA_RELEASED_OLD = "https://tea.texas.gov/student-assessment/staar/released-test-questions/"
# 2018 and older forms were on TEA's site until about 2021 and are now gone from it. The Internet Archive keeps
# TEA's original files; the user opens these links and saves the PDFs by hand, as with TEA's own links.
TEA_FILES = "https://tea.texas.gov/sites/default/files/"


def _archived(snapshot: str, name: str) -> tuple[str, str]:
    """(archive link that serves the file as TEA published it, TEA's original link)."""
    return f"https://web.archive.org/web/{snapshot}id_/{TEA_FILES}{name}", TEA_FILES + name

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
    # Algebra I end-of-course tests (most students take Algebra I in grade 9). 54 items each, 5 of them gridded.
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
    # Math TEKS changed in 2014-15 (first tested in 2015 for grades 3-8, 2016 for Algebra I). Forms from 2016 on
    # test today's math TEKS and keep their codes, so they are imported. Older forms are in SKIPPED_TESTS.
    *(
        ReleasedTest(
            "TX", "STAAR", "G3", year, "spring", url, tagged=False, archived_from=tea,
            key_url=_archived(key_snapshot, key)[0], items=46 if year == 2016 else 32,
        )
        # The 2016 form is longer: 46 items (3 gridded); 2017 on have 32.
        for year, (url, tea), key_snapshot, key in [
            (2018, _archived("20201021073255", "2018_STAAR_Gr3_Math_Test.pdf"),
             "20201021034242", "2018_STAAR_Gr3_Math_Key_Paper_tagged.pdf"),
            (2017, _archived("20201020070638", "STAAR_G3-2017-Test-Math-f.pdf"),
             "20201020070718", "2017_STAAR_Gr3Math_Key_Paper_tagged.pdf"),
            (2016, _archived("20201020132730", "STAAR-G3-2016Test-Math-f.pdf"),
             "20201020085845", "STAAR-2016-Key-G3-Math.pdf"),
        ]
    ),
    *(
        ReleasedTest(
            "TX", "STAAR", "A1", year, "spring", url, tagged=False, items=54, gridded_kind="Decimal",
            archived_from=tea, key_url=_archived(key_snapshot, key)[0],
        )
        for year, (url, tea), key_snapshot, key in [
            (2018, _archived("20201023160952", "2018_STAAR_Algebra_I_Test.pdf"),
             "20201020090732", "2018_STAAR_Algebra_I_Key_Paper_tagged.pdf"),
            (2017, _archived("20201020105444", "STAAR-EOC-2017-Test-AlgebraI-f.pdf"),
             "20201021074124", "2017_STAAR_AlgebraI_Key_Paper_tagged.pdf"),
            (2016, _archived("20201023215043", "STAAR-EOC-2016Test-Algebra%20I-f.pdf"),
             "20201021001527", "STAAR-2016-Key-Algebra%20I.pdf"),
        ]
    ),
]


@dataclass(frozen=True)
class SkippedTest:
    """A released form we know about and leave out on purpose, so the TEA page check does not report it as new."""

    jurisdiction: str
    exam: str
    level: str
    subject: str
    years: tuple[int, ...]
    reason: str


OLD_MATH = "tests the math TEKS from before 2015, whose skill codes mean something else today"
OLD_READING = "tests the reading TEKS from before 2017, whose skill codes mean something else today"
ONLINE_ONLY = "online test with new item types; TEA posts only its answer key as a PDF"

SKIPPED_TESTS: list[SkippedTest] = [
    SkippedTest("TX", "STAAR", "G3", "math", (2013, 2014), OLD_MATH),
    SkippedTest("TX", "STAAR", "A1", "math", (2013, 2014, 2015), OLD_MATH),
    SkippedTest("TX", "STAAR", "G3", "reading", (2013, 2014, 2015, 2016, 2017, 2018, 2019), OLD_READING),
    *(SkippedTest("TX", "STAAR", level, subject, (2023, 2024, 2025, 2026), ONLINE_ONLY)
      for level, subject in [("G3", "math"), ("G3", "reading"), ("A1", "math")]),
]
