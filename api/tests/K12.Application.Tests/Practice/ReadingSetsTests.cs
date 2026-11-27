using K12.Application.ContentImport.ImportExamForm;
using K12.Application.Practice.Sets;
using K12.Domain.Content;
using K12.Domain.Content.Answers;
using K12.Domain.Curriculum;

namespace K12.Application.Tests.Practice;

/// <summary>Reading: passages shown beside their questions, item-level categories, and the full practice test.</summary>
public class ReadingSetsTests
{
    /// <summary>
    /// A reading course: vocabulary (category 1), plot (2), text features (3) and inference (no category: literary or
    /// informational by selection). Two years, each with a story (3 items) and an article (3 items).
    /// </summary>
    private sealed class Library
    {
        public TestStore Store { get; } = new();
        public Course Course { get; }
        public Skill Vocabulary { get; }
        public Skill Plot { get; }
        public Skill Features { get; }
        public Skill Inference { get; }
        public List<ExamForm> Forms { get; } = [];
        public List<Passage> Passages { get; } = [];

        public Library(bool withPracticeTest = true)
        {
            Course = Course.Create("TX", "reading", "G3", "Grade 3", "FW", "Framework",
                [new ReportingCategory(1, "Across genres"), new ReportingCategory(2, "Literary"), new ReportingCategory(3, "Informational")]);
            if (withPracticeTest)
                Course.DescribePracticeTest(new PracticeTestBlueprint(6, 240,
                    [new CategoryShare(1, 2), new CategoryShare(2, 2), new CategoryShare(3, 2)]));
            Vocabulary = Skill.Define(Course, "3.3(B)", "Words in context", 1, "readiness", "{}");
            Plot = Skill.Define(Course, "3.8(C)", "Plot", 2, "readiness", "{}");
            Features = Skill.Define(Course, "3.9(D)", "Text features", 3, "readiness", "{}");
            Inference = Skill.Define(Course, "3.6(F)", "Inferences", null, "readiness", "{}");
            Store.Courses.Add(Course);
            Store.Skills.AddRange([Vocabulary, Plot, Features, Inference]);

            foreach (var year in new[] { 2021, 2022 })
            {
                var form = ExamForm.Create(Course.Id, "EXAM", year, "spring", "https://example.org/r.pdf");
                var story = AddPassage($"Story {year}", 2);
                var article = AddPassage($"Article {year}", 1);
                // Printed order: story items 1-3, article items 4-6. Inference counts as literary or informational by passage.
                Add(form, 1, story, Vocabulary, 1);
                Add(form, 2, story, Plot, 2);
                Add(form, 3, story, Inference, 2);
                Add(form, 4, article, Features, 3);
                Add(form, 5, article, Inference, 3);
                Add(form, 6, article, Vocabulary, 1);
                Forms.Add(form);
                Store.Forms.Add(form);
            }
        }

        private Passage AddPassage(string title, int pages)
        {
            var paths = string.Join(",", Enumerable.Range(1, pages).Select(n => $"\"{title}-{n}.png\""));
            var p = Passage.Create(Course.Id, title, $$"""{"pages":[{{paths}}],"text":"Once upon a time.","textNeedsReview":true}""", QuestionOrigin.Official);
            Passages.Add(p);
            Store.Passages.Add(p);
            return p;
        }

        private void Add(ExamForm form, int number, Passage passage, Skill skill, int category)
        {
            var q = Question.Draft([skill.Id], "choice", $$"""{"stem":"Item {{number}}","choices":[{"label":"A","text":"a"},{"label":"B","text":"b"}]}""",
                new AnswerSpec { Kind = AnswerKind.Choice, Value = "A" }, Difficulty.OnLevel, QuestionOrigin.Official,
                passageId: passage.Id, reportingCategory: category);
            q.PublishReleased(Store.UtcNow);
            Store.Questions.Add(q);
            form.AddItem(number, q.Id);
        }

        public GetPracticeSetHandler Sets() => new(Store, Store, Store, Store, Store);

        public Task<K12.Application.Abstractions.Result<PracticeSet>> Get(SetSource source, int? category = null, int? count = null, int seed = 7, Guid? formId = null) =>
            Sets().HandleAsync(new GetPracticeSetQuery(Course.Id.Value, source, formId, category, null, count, seed));
    }

    /// <summary>Every passage's questions sit together, and each passage the questions use is in the set once.</summary>
    private static void AssertPassagesTogether(PracticeSet set)
    {
        var order = set.Questions.Select(q => q.PassageId).ToList();
        var runs = order.Where((id, i) => i == 0 || order[i - 1] != id).ToList();
        Assert.Equal(runs.Count, runs.Distinct().Count());
        Assert.Equal(runs.Where(id => id is not null).Cast<Guid>(), (set.Passages ?? []).Select(p => p.Id));
    }

    [Fact]
    public async Task A_year_comes_with_its_passages_in_order_and_items_in_printed_order()
    {
        var lib = new Library();
        var set = (await lib.Get(SetSource.Year, formId: lib.Forms[1].Id.Value)).Value!;

        Assert.Equal(6, set.Questions.Count);
        Assert.Equal(["Story 2022", "Article 2022"], set.Passages!.Select(p => p.Title));
        Assert.Equal([2, 1], set.Passages!.Select(p => p.PageCount));
        Assert.All(set.Passages!, p => Assert.Null(p.Text)); // text still needs checking, so pictures only
        Assert.Equal(["Item 1", "Item 2", "Item 3", "Item 4", "Item 5", "Item 6"], set.Questions.Select(q => q.Stem));
        Assert.Null(set.TimeLimitMinutes);
        AssertPassagesTogether(set);
    }

    [Theory]
    [InlineData(1, 4)]
    [InlineData(2, 2)]
    public async Task Mixed_practice_takes_whole_passages(int count, int seed)
    {
        var lib = new Library();
        var set = (await lib.Get(SetSource.Shuffle, count: count, seed: seed)).Value!;

        // At least one whole passage, never part of one.
        Assert.Equal(3, set.Questions.Count);
        Assert.Single(set.Passages!);
        var stems = set.Questions.Select(q => q.Stem).Order().ToList();
        Assert.True(stems.SequenceEqual(["Item 1", "Item 2", "Item 3"]) || stems.SequenceEqual(["Item 4", "Item 5", "Item 6"]));
        AssertPassagesTogether(set);
    }

    [Theory]
    [InlineData(1, new[] { "Item 1", "Item 6" })]
    [InlineData(2, new[] { "Item 2", "Item 3" })]
    [InlineData(3, new[] { "Item 4", "Item 5" })]
    public async Task A_category_uses_each_items_own_category(int category, string[] stems)
    {
        var lib = new Library();
        var set = (await lib.Get(SetSource.Topic, category: category, count: 20)).Value!;

        Assert.Equal(4, set.Questions.Count); // two per year
        Assert.All(set.Questions, q => Assert.Equal(category, q.Category));
        Assert.Equal(stems, set.Questions.Select(q => q.Stem).Distinct().Order());
        AssertPassagesTogether(set);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task The_full_practice_test_mirrors_the_blueprint(int seed)
    {
        var lib = new Library();
        var set = (await lib.Get(SetSource.PracticeTest, seed: seed)).Value!;

        Assert.Equal(6, set.Questions.Count);
        Assert.Equal(240, set.TimeLimitMinutes);
        Assert.Equal(SetSource.PracticeTest, set.Source);
        Assert.Equal(2, set.Passages!.Count);
        Assert.Equal(2, set.Questions.Count(q => q.Category == 1));
        AssertPassagesTogether(set);
        // Within a passage, the items come in printed order.
        foreach (var group in set.Questions.GroupBy(q => q.PassageId))
            Assert.Equal(group.Select(q => q.Source!.ItemNumber).Order(), group.Select(q => q.Source!.ItemNumber));
    }

    [Fact]
    public async Task A_course_without_a_blueprint_has_no_practice_test()
    {
        var lib = new Library(withPracticeTest: false);
        Assert.Equal("practice.no_practice_test", (await lib.Get(SetSource.PracticeTest)).Error!.Code);
    }

    [Fact]
    public async Task A_math_practice_test_follows_the_category_mix()
    {
        var store = new TestStore();
        var course = Course.Create("TX", "math", "G3", "Grade 3", "FW", "Framework", [new ReportingCategory(1, "A"), new ReportingCategory(2, "B")]);
        course.DescribePracticeTest(new PracticeTestBlueprint(5, 240, [new CategoryShare(1, 2), new CategoryShare(2, 3)]));
        var a = Skill.Define(course, "1", "A", 1, null, "{}");
        var b = Skill.Define(course, "2", "B", 2, null, "{}");
        store.Courses.Add(course);
        store.Skills.AddRange([a, b]);
        for (var n = 0; n < 10; n++)
        {
            var q = Question.Draft([n % 2 == 0 ? a.Id : b.Id], "numeric", "{}", new AnswerSpec { Kind = AnswerKind.Integer, Value = "1" },
                Difficulty.OnLevel, QuestionOrigin.Official);
            q.PublishReleased(store.UtcNow);
            store.Questions.Add(q);
        }

        var set = (await new GetPracticeSetHandler(store, store, store, store, store)
            .HandleAsync(new GetPracticeSetQuery(course.Id.Value, SetSource.PracticeTest, Seed: 3))).Value!;

        Assert.Equal(5, set.Questions.Count);
        Assert.Equal(2, set.Questions.Count(q => q.Category == 1));
        Assert.Equal(3, set.Questions.Count(q => q.Category == 2));
        Assert.Empty(set.Passages!);
    }

    [Fact]
    public async Task The_catalog_counts_by_item_category_and_offers_the_practice_test()
    {
        var lib = new Library();
        var course = Assert.Single((await new GetPracticeCatalogHandler(lib.Store, lib.Store, lib.Store, lib.Store)
            .HandleAsync(new GetPracticeCatalogQuery())).Value!.Courses);

        Assert.Equal([4, 4, 4], course.Categories.Select(c => c.QuestionCount));
        // Inference has no category of its own, so it is listed wherever its items count.
        Assert.Equal(["3.3(B)"], course.Categories[0].Skills.Select(s => s.Code));
        Assert.Equal(["3.6(F)", "3.8(C)"], course.Categories[1].Skills.Select(s => s.Code));
        Assert.Equal(["3.6(F)", "3.9(D)"], course.Categories[2].Skills.Select(s => s.Code));
        Assert.Equal(new CatalogPracticeTest(6, 240, 12), course.PracticeTest);
        Assert.True(course.HasPassages);
        Assert.Equal("en", course.Language);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    [InlineData(0, false)]
    public async Task Passage_pages_are_served_by_number(int page, bool found)
    {
        var lib = new Library();
        var story = lib.Passages[0];
        lib.Store.Images[$"{story.Title}-1.png"] = [1];
        lib.Store.Images[$"{story.Title}-2.png"] = [2];

        var result = await new GetPassagePageHandler(lib.Store, lib.Store, lib.Store).HandleAsync(new GetPassagePageQuery(story.Id.Value, page));

        Assert.Equal(found, result.IsSuccess);
    }

    [Fact]
    public async Task A_generated_passage_is_shown_only_once_a_live_question_is_about_it()
    {
        var lib = new Library();
        var generated = Passage.Create(lib.Course.Id, "New", """{"pages":["new-1.png"]}""", QuestionOrigin.Generated);
        lib.Store.Passages.Add(generated);
        lib.Store.Images["new-1.png"] = [1];
        var handler = new GetPassagePageHandler(lib.Store, lib.Store, lib.Store);

        Assert.False((await handler.HandleAsync(new GetPassagePageQuery(generated.Id.Value, 1))).IsSuccess);

        var q = Question.Draft([lib.Plot.Id], "choice", "{}", new AnswerSpec { Kind = AnswerKind.Choice, Value = "A" },
            Difficulty.OnLevel, QuestionOrigin.Generated, passageId: generated.Id);
        q.SubmitForAiReview();
        lib.Store.Questions.Add(q);
        Assert.False((await handler.HandleAsync(new GetPassagePageQuery(generated.Id.Value, 1))).IsSuccess);
    }

    [Fact]
    public async Task Importing_a_reading_test_creates_its_passages_and_links_its_items()
    {
        var lib = new Library();
        var s = lib.Store;
        var handler = new ImportExamFormHandler(s, s, s, s, s, s, s);
        var before = s.Passages.Count;
        var item = (int n, string? passage, int? category) => new ImportItem(
            n, ["3.6(F)"], "choice", """{"stem":"x"}""", new AnswerSpec { Kind = AnswerKind.Choice, Value = "A" }, passage, category);

        var ok = await handler.HandleAsync(new ImportExamFormCommand("TX", "reading", "G3", "EXAM", 2019, "spring", "u",
            [item(1, "p1", 2), item(2, "p1", 2), item(3, "p2", 3)],
            [new ImportPassage("p1", "Story", """{"pages":["a.png"]}"""), new ImportPassage("p2", "Article", "{}")]));

        Assert.True(ok.IsSuccess, ok.Error?.Message);
        Assert.Equal(before + 2, s.Passages.Count);
        var story = s.Passages.Single(p => p.Title == "Story");
        var imported = s.Questions.TakeLast(3).ToList();
        Assert.Equal([story.Id, story.Id], imported.Take(2).Select(q => q.PassageId!.Value));
        Assert.Equal([2, 2, 3], imported.Select(q => q.ReportingCategory!.Value));
    }

    public static TheoryData<string?, int?, ImportPassage[], string> BadImports => new()
    {
        { "p9", null, [new ImportPassage("p1", "Story", "{}")], "import.unknown_passage" },
        { "p1", 7, [new ImportPassage("p1", "Story", "{}")], "import.unknown_category" },
        { "p1", null, [new ImportPassage("p1", "Story", "{}"), new ImportPassage("p1", "Again", "{}")], "import.duplicate_passage" },
        { "p1", null, [new ImportPassage("p1", "", "{}")], "import.invalid_item" },
    };

    [Theory]
    [MemberData(nameof(BadImports))]
    public async Task A_reading_import_that_does_not_add_up_is_refused(string? passage, int? category, ImportPassage[] passages, string code)
    {
        var lib = new Library();
        var s = lib.Store;
        var result = await new ImportExamFormHandler(s, s, s, s, s, s, s).HandleAsync(new ImportExamFormCommand(
            "TX", "reading", "G3", "EXAM", 2019, "spring", "u",
            [new ImportItem(1, ["3.6(F)"], "choice", "{}", new AnswerSpec { Kind = AnswerKind.Choice, Value = "A" }, passage, category)],
            passages));
        Assert.Equal(code, result.Error!.Code);
    }
}
