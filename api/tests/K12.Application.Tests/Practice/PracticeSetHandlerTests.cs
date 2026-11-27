using K12.Application.Practice.Sets;
using K12.Domain.Content;
using K12.Domain.Content.Answers;
using K12.Domain.Curriculum;

namespace K12.Application.Tests.Practice;

public class PracticeSetHandlerTests
{
    /// <summary>A course with two categories, a 2022 form of 6 Live items, a retired item, and a generated question still in review.</summary>
    private sealed class Bank
    {
        public TestStore Store { get; } = new();
        public Course Course { get; }
        public Skill Numbers { get; }
        public Skill Shapes { get; }
        public ExamForm Form { get; }
        public Question Retired { get; }
        public Question InReview { get; }

        public Bank()
        {
            Course = Course.Create("TX", "math", "G3", "Grade 3", "FW", "Framework",
                [new ReportingCategory(1, "Numbers"), new ReportingCategory(2, "Shapes")]);
            Course.NameJurisdiction("Texas");
            Numbers = Skill.Define(Course, "3.2(A)", "Place value", 1, "readiness", "{}");
            Shapes = Skill.Define(Course, "3.6(A)", "Shapes", 2, "supporting", "{}");
            Store.Courses.Add(Course);
            Store.Skills.AddRange([Numbers, Shapes]);

            Form = ExamForm.Create(Course.Id, "EXAM", 2022, "spring", "https://example.org/t.pdf");
            for (var n = 1; n <= 6; n++)
            {
                var q = Official(n % 2 == 1 ? Numbers : Shapes, n);
                Form.AddItem(n, q.Id);
            }

            Retired = Official(Numbers, 7);
            Retired.Retire();
            Form.AddItem(7, Retired.Id);
            Store.Forms.Add(Form);

            InReview = Question.Draft([Numbers.Id], "numeric", """{"stem":"draft"}""",
                new AnswerSpec { Kind = AnswerKind.Integer, Value = "1" }, Difficulty.OnLevel, QuestionOrigin.Generated);
            InReview.SubmitForAiReview();
            Store.Questions.Add(InReview);
        }

        private Question Official(Skill skill, int n)
        {
            var content = n == 1
                ? """{"image":"form/01.png","stem":"Which is 3/4?","choices":[{"label":"A","text":"1/4"},{"label":"B","text":"3/4"}],"explanation":"Count the parts."}"""
                : $$"""{"image":"form/{{n:00}}.png","stem":"Item {{n}}","textNeedsReview":true}""";
            var answer = n == 1 ? new AnswerSpec { Kind = AnswerKind.Choice, Value = "B" } : new AnswerSpec { Kind = AnswerKind.Integer, Value = $"{n}" };
            var q = Question.Draft([skill.Id], n == 1 ? "choice" : "numeric", content, answer, Difficulty.OnLevel, QuestionOrigin.Official);
            q.PublishReleased(Store.UtcNow);
            Store.Questions.Add(q);
            return q;
        }

        public GetPracticeSetHandler Sets() => new(Store, Store, Store, Store, Store);
    }

    [Fact]
    public async Task Pick_a_year_gives_every_live_item_in_printed_order()
    {
        var bank = new Bank();
        var set = (await bank.Sets().HandleAsync(new GetPracticeSetQuery(bank.Course.Id.Value, SetSource.Year, bank.Form.Id.Value))).Value!;

        Assert.Equal("EXAM 2022", set.Title);
        Assert.Equal([1, 2, 3, 4, 5, 6], set.Questions.Select(q => q.Source!.ItemNumber));
        Assert.DoesNotContain(set.Questions, q => q.Id == bank.Retired.Id.Value);
    }

    [Theory]
    [InlineData(1, null, 3)]
    [InlineData(2, null, 3)]
    [InlineData(null, "3.6(a)", 3)]
    public async Task A_topic_set_holds_only_that_topics_live_questions(int? category, string? skill, int expected)
    {
        var bank = new Bank();
        var set = (await bank.Sets().HandleAsync(new GetPracticeSetQuery(bank.Course.Id.Value, SetSource.Topic, Category: category, SkillCode: skill))).Value!;

        Assert.Equal(expected, set.Questions.Count);
        var wanted = skill is null ? category : 2;
        Assert.All(set.Questions, q => Assert.Equal(wanted, q.Category));
        Assert.DoesNotContain(set.Questions, q => q.Id == bank.InReview.Id.Value || q.Id == bank.Retired.Id.Value);
    }

    [Fact]
    public async Task A_shuffle_takes_the_count_asked_for_and_the_same_seed_gives_the_same_order()
    {
        var bank = new Bank();
        var query = new GetPracticeSetQuery(bank.Course.Id.Value, SetSource.Shuffle, Count: 4, Seed: 42);
        var first = (await bank.Sets().HandleAsync(query)).Value!;
        var again = (await bank.Sets().HandleAsync(query)).Value!;

        Assert.Equal(4, first.Questions.Count);
        Assert.Equal(first.Questions.Select(q => q.Id), again.Questions.Select(q => q.Id));
        Assert.Equal(6, (await bank.Sets().HandleAsync(query with { Count = 100 })).Value!.Questions.Count);
    }

    [Theory]
    [InlineData(SetSource.Year, "practice.no_form")]
    [InlineData(SetSource.Topic, "practice.no_topic")]
    public async Task A_set_needs_what_it_is_made_from(SetSource source, string error)
    {
        var bank = new Bank();
        var result = await bank.Sets().HandleAsync(new GetPracticeSetQuery(bank.Course.Id.Value, source));
        Assert.Equal(error, result.Error?.Code);
    }

    [Fact]
    public async Task An_unknown_course_is_not_found() =>
        Assert.Equal("practice.no_course",
            (await new Bank().Sets().HandleAsync(new GetPracticeSetQuery(Guid.NewGuid(), SetSource.Shuffle))).Error?.Code);

    [Fact]
    public async Task Students_see_the_question_but_never_its_answer_or_explanation()
    {
        var bank = new Bank();
        var set = (await bank.Sets().HandleAsync(new GetPracticeSetQuery(bank.Course.Id.Value, SetSource.Year, bank.Form.Id.Value))).Value!;
        var first = set.Questions[0];

        Assert.Equal(("Which is 3/4?", AnswerKind.Choice, true), (first.Stem, first.AnswerKind, first.HasImage));
        Assert.Equal(["A", "B"], first.Choices.Select(c => c.Label));
        Assert.Equal("3.2(A)", first.SkillCode);
        Assert.Equal(new QuestionSourceView("EXAM", 2022, "spring", 1), first.Source);
        // Rough PDF text always comes with its picture.
        Assert.All(set.Questions.Skip(1), q => Assert.True(q.TextNeedsReview && q.ShowImage));
        Assert.DoesNotContain(typeof(StudentQuestion).GetProperties(), p => p.Name is "Answer" or "Explanation" or "CorrectAnswer");
    }

    public static TheoryData<string, string, bool, string?> ChoiceTexts => new()
    {
        // The importer checked the choices are plain text: show them, and say the value with the key.
        { "checked choices", """{"textNeedsReview":true,"choicesAreText":true,"choices":[{"label":"A","text":"28"},{"label":"B","text":"7"}]}""", true, "7" },
        // Text read from a picture choice is never shown.
        { "picture choices", """{"textNeedsReview":true,"choicesAreText":false,"choices":[{"label":"A","text":"x"},{"label":"B","text":"y"}]}""", false, null },
        // Older imports carry no flag: their text was rough.
        { "older import", """{"textNeedsReview":true,"choices":[{"label":"A","text":"28"},{"label":"B","text":"7"}]}""", false, null },
        // Reviewed content (generated, owner approved) has no flag and clean text.
        { "reviewed content", """{"choices":[{"label":"A","text":"28"},{"label":"B","text":"7"}]}""", true, "7" },
        // A blank choice means the text is not the whole choice.
        { "a blank choice", """{"choicesAreText":true,"choices":[{"label":"A","text":""},{"label":"B","text":"7"}]}""", false, null },
    };

    [Theory]
    [MemberData(nameof(ChoiceTexts))]
    public async Task Choice_text_shows_on_the_buttons_and_in_the_key_only_when_it_is_the_whole_choice(
        string _, string content, bool shown, string? keyText)
    {
        var store = new TestStore();
        var course = Course.Create("TX", "math", "G3", "Grade 3", "FW", "Framework", [new ReportingCategory(1, "Numbers")]);
        var skill = Skill.Define(course, "3.2(A)", "Place value", 1, "readiness", "{}");
        store.Courses.Add(course);
        store.Skills.Add(skill);
        var q = Question.Draft([skill.Id], "choice", content, new AnswerSpec { Kind = AnswerKind.Choice, Value = "B" },
            Difficulty.OnLevel, QuestionOrigin.Official);
        q.PublishReleased(store.UtcNow);
        store.Questions.Add(q);

        var set = (await new GetPracticeSetHandler(store, store, store, store, store)
            .HandleAsync(new GetPracticeSetQuery(course.Id.Value, SetSource.Shuffle))).Value!;
        var scored = (await new ScoreResponsesHandler(store).HandleAsync(new ScoreResponsesCommand([new ResponseItem(q.Id.Value, "A")]))).Value!;

        Assert.Equal(shown, set.Questions.Single().ChoicesAreText);
        Assert.Equal(keyText, scored.Items.Single().CorrectChoiceText);
    }

    [Fact]
    public async Task The_catalog_counts_live_questions_by_topic_and_by_test()
    {
        var bank = new Bank();
        var catalog = (await new GetPracticeCatalogHandler(bank.Store, bank.Store, bank.Store, bank.Store)
            .HandleAsync(new GetPracticeCatalogQuery())).Value!;

        var course = Assert.Single(catalog.Courses);
        Assert.Equal(("Texas", 6), (course.JurisdictionName, course.QuestionCount));
        Assert.Equal([3, 3], course.Categories.Select(c => c.QuestionCount));
        Assert.Equal(3, course.Categories[0].Skills.Single().QuestionCount);
        Assert.Equal(6, Assert.Single(course.Forms).QuestionCount);
    }

    [Fact]
    public async Task The_catalog_lists_grades_and_courses_in_school_order()
    {
        var bank = new Bank();
        bank.Course.Place(CourseKind.Grade, 3);
        var algebra = Course.Create("TX", "math", "A1", "Algebra I", "FW", "Framework", [new ReportingCategory(1, "Numbers")]);
        algebra.Place(CourseKind.Course, 9);
        var grade9 = Course.Create("TX", "math", "G9", "Grade 9", "FW", "Framework", [new ReportingCategory(1, "Numbers")]);
        grade9.Place(CourseKind.Grade, 9);
        var unplaced = Course.Create("TX", "math", "ZZ", "Another", "FW", "Framework", [new ReportingCategory(1, "Numbers")]);
        bank.Store.Courses.InsertRange(0, [unplaced, algebra, grade9]);

        var catalog = (await new GetPracticeCatalogHandler(bank.Store, bank.Store, bank.Store, bank.Store)
            .HandleAsync(new GetPracticeCatalogQuery())).Value!;

        Assert.Equal(["G3", "G9", "A1", "ZZ"], catalog.Courses.Select(c => c.Code));
        var a1 = catalog.Courses.Single(c => c.Code == "A1");
        Assert.Equal(("Course", (int?)9), (a1.Kind, a1.TypicalGrade));
    }

    public static TheoryData<int, string?, bool, bool, string, string?> Responses => new()
    {
        // item, response, answered, correct, key, choice text
        { 1, "B", true, true, "B", "3/4" },
        { 1, "a", true, false, "B", "3/4" },
        { 1, "", false, false, "B", "3/4" },
        { 1, null, false, false, "B", "3/4" },
        { 2, "2", true, true, "2", null },
        { 2, " 2 ", true, true, "2", null },
        { 2, "3", true, false, "2", null },
    };

    [Theory]
    [MemberData(nameof(Responses))]
    public async Task Scoring_reveals_the_key_with_each_result(int item, string? response, bool answered, bool correct, string key, string? choiceText)
    {
        var bank = new Bank();
        var questionId = bank.Form.Items.Single(i => i.Number == item).QuestionId.Value;
        var result = (await new ScoreResponsesHandler(bank.Store).HandleAsync(new ScoreResponsesCommand([new ResponseItem(questionId, response)]))).Value!;

        var scored = Assert.Single(result.Items);
        Assert.Equal((answered, correct, key, choiceText), (scored.Answered, scored.IsCorrect, scored.CorrectAnswer, scored.CorrectChoiceText));
        Assert.Equal(correct ? 1 : 0, result.Correct);
        Assert.Equal(item == 1 ? "Count the parts." : null, scored.Explanation);
    }

    [Fact]
    public async Task Scoring_refuses_questions_students_cannot_see()
    {
        var bank = new Bank();
        var handler = new ScoreResponsesHandler(bank.Store);
        Assert.Equal("question.not_found", (await handler.HandleAsync(new ScoreResponsesCommand([new ResponseItem(bank.InReview.Id.Value, "1")]))).Error?.Code);
        Assert.Equal("question.not_found", (await handler.HandleAsync(new ScoreResponsesCommand([new ResponseItem(bank.Retired.Id.Value, "7")]))).Error?.Code);
        Assert.Equal("score.empty", (await handler.HandleAsync(new ScoreResponsesCommand([]))).Error?.Code);
    }

    [Fact]
    public async Task Students_get_pictures_of_live_questions_only()
    {
        var bank = new Bank();
        bank.Store.Images["form/01.png"] = [1, 2, 3];
        bank.Store.Images["form/07.png"] = [7];
        var handler = new GetQuestionImageHandler(bank.Store, bank.Store);
        var live = bank.Form.Items.Single(i => i.Number == 1).QuestionId.Value;

        Assert.True((await handler.HandleAsync(new GetQuestionImageQuery(live))).IsSuccess);
        Assert.Equal("image.not_found", (await handler.HandleAsync(new GetQuestionImageQuery(bank.Retired.Id.Value))).Error?.Code);
        var noPicture = bank.Form.Items.Single(i => i.Number == 2).QuestionId.Value;
        Assert.Equal("image.not_found", (await handler.HandleAsync(new GetQuestionImageQuery(noPicture))).Error?.Code);
    }
}
