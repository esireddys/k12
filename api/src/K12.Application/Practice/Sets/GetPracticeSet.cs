using K12.Application.Abstractions;
using K12.Domain.Content;
using K12.Domain.Curriculum;

namespace K12.Application.Practice.Sets;

/// <summary>Where a set's questions come from.</summary>
public enum SetSource
{
    /// <summary>A random mix of the course's questions from every year.</summary>
    Shuffle,
    /// <summary>One released test, every item in its printed order.</summary>
    Year,
    /// <summary>One reporting category or one skill, in random order.</summary>
    Topic,
    /// <summary>A kid's own missed questions, until they answer each one right. Kids only, never guests.</summary>
    Mistakes,
    /// <summary>
    /// A full-length test shaped like the real statewide one (item count, category mix, time limit), drawn from
    /// every released year. Only for courses whose curriculum data describes the test.
    /// </summary>
    PracticeTest,
}

/// <param name="Seed">Makes a shuffle repeatable, so reloading the page gives the same set.</param>
public sealed record GetPracticeSetQuery(
    Guid CourseId, SetSource Source, Guid? FormId = null, int? Category = null, string? SkillCode = null,
    int? Count = null, int? Seed = null);

/// <param name="Title">What the set is, for the screen's header, for example "2022" or a category name. From data.</param>
/// <param name="Passages">The reading selections the questions are about, in the order they come up.</param>
/// <param name="TimeLimitMinutes">How long the student gets for the whole set, when it is timed like the real test.</param>
public sealed record PracticeSet(
    Guid CourseId, SetSource Source, string Title, IReadOnlyList<StudentQuestion> Questions,
    IReadOnlyList<PassageView>? Passages = null, int? TimeLimitMinutes = null);

/// <summary>Picks the questions for one practice or exam session. Only Live questions, never answers.</summary>
public sealed class GetPracticeSetHandler(
    ICourseRepository courses, ISkillRepository skills, IExamFormRepository forms, IQuestionRepository questions,
    IPassageRepository passages)
    : IQueryHandler<GetPracticeSetQuery, PracticeSet>
{
    public const int DefaultCount = 10;
    public const int MaxCount = 60;

    public async Task<Result<PracticeSet>> HandleAsync(GetPracticeSetQuery query, CancellationToken ct = default)
    {
        var course = (await courses.ListByIdsAsync([new CourseId(query.CourseId)], ct)).FirstOrDefault();
        if (course is null) return Error.NotFound("practice.no_course", "No such course.");

        var courseSkills = await skills.ListByCourseAsync(course.Id, ct);
        var skillsById = courseSkills.ToDictionary(s => s.Id);
        var count = Math.Clamp(query.Count ?? DefaultCount, 1, MaxCount);
        var random = new Random(query.Seed ?? Random.Shared.Next());

        IReadOnlyList<Question> picked;
        string title;
        int? minutes = null;
        switch (query.Source)
        {
            case SetSource.Year:
            {
                if (query.FormId is not { } formId) return Error.Validation("practice.no_form", "Pick a year's test.");
                var form = await forms.GetAsync(new ExamFormId(formId), ct);
                if (form is null || form.CourseId != course.Id) return Error.NotFound("practice.no_form", "No such test.");
                var byId = (await questions.ListByIdsAsync(form.Items.Select(i => i.QuestionId).ToList(), ct)).ToDictionary(q => q.Id);
                picked = form.Items
                    .Where(i => byId.TryGetValue(i.QuestionId, out var q) && q.IsVisibleToStudents)
                    .Select(i => byId[i.QuestionId]).ToList();
                title = $"{form.Exam} {form.Year}";
                break;
            }
            case SetSource.Topic:
            {
                IReadOnlyCollection<SkillId> topicSkills;
                if (!string.IsNullOrWhiteSpace(query.SkillCode))
                {
                    var skill = courseSkills.FirstOrDefault(s => string.Equals(s.Code, query.SkillCode.Trim(), StringComparison.OrdinalIgnoreCase));
                    if (skill is null) return Error.NotFound("practice.no_skill", "No such skill in this course.");
                    topicSkills = [skill.Id];
                    title = skill.Description.Length > 0 ? skill.Description : skill.Code;
                }
                else if (query.Category is { } number && course.Categories.FirstOrDefault(c => c.Number == number) is { } category)
                {
                    // By the category each item counts toward, which for reading depends on the passage, not only the skill.
                    var inCategory = (await LiveQuestions.ForSkillsAsync(questions, skillsById.Keys, ct))
                        .Where(q => StudentQuestions.CategoryOf(q, skillsById) == number).ToList();
                    picked = SetPassages.GroupByPassage(Shuffle(inCategory, random).Take(count));
                    title = category.Name;
                    break;
                }
                else
                {
                    return Error.Validation("practice.no_topic", "Pick a topic.");
                }

                picked = SetPassages.GroupByPassage(Shuffle(await LiveQuestions.ForSkillsAsync(questions, topicSkills, ct), random).Take(count));
                break;
            }
            case SetSource.Shuffle:
                // Whole passages at a time, so a student reads a few selections and answers all of their questions.
                picked = PracticeTests.TakeGroups(
                    Shuffle(await LiveQuestions.ForSkillsAsync(questions, skillsById.Keys, ct), random), count);
                title = "Mixed";
                break;
            case SetSource.PracticeTest:
            {
                if (course.PracticeTest is not { } blueprint)
                    return Error.NotFound("practice.no_practice_test", "This course has no full practice test yet.");
                var live = Shuffle(await LiveQuestions.ForSkillsAsync(questions, skillsById.Keys, ct), random);
                picked = PracticeTests.Build(blueprint, live, q => StudentQuestions.CategoryOf(q, skillsById), random);
                title = PracticeTests.Title;
                minutes = blueprint.Minutes;
                break;
            }
            default:
                return Error.Validation("practice.bad_source", "Unknown kind of set.");
        }

        var sources = StudentQuestions.Sources(await forms.ListContainingAsync(picked.Select(q => q.Id).ToList(), ct));
        picked = SetPassages.InPrintedOrder(picked, sources);
        return new PracticeSet(
            course.Id.Value, query.Source, title,
            picked.Select(q => StudentQuestions.View(q, skillsById, sources)).ToList(),
            await SetPassages.ForAsync(picked, passages, ct), minutes);
    }

    internal static List<Question> Shuffle(IReadOnlyList<Question> questions, Random random)
    {
        // Sort by id first so the same seed gives the same order whatever order the store returned.
        var list = questions.OrderBy(q => q.Id.Value).ToList();
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
        return list;
    }
}
