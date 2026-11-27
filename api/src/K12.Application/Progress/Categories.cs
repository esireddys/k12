using K12.Application.Practice.Sets;
using K12.Domain.Accounts;
using K12.Domain.Content;
using K12.Domain.Curriculum;
using K12.Domain.Progress;

namespace K12.Application.Progress;

/// <summary>How a kid is doing in one reporting category. Names come from curriculum data.</summary>
/// <param name="Accuracy">Share of all answers in the category that were right, 0 to 100; empty before any answer.</param>
/// <param name="QuestionCount">Live questions in the category, so a parent sees how much there is to practice.</param>
public sealed record CategoryView(
    int Number, string Name, MasteryLevel Level, int Answered, int Correct, int? Accuracy, int QuestionCount);

internal static class CategoryViews
{
    /// <summary>The kid's standing in every category of the course, from all their answers in it.</summary>
    public static async Task<IReadOnlyList<CategoryView>> ForAsync(
        KidProfileId kidId, Course course, IReadOnlyList<Skill> courseSkills, IReadOnlyList<Question> liveInCourse,
        IPracticeSessionRepository sessions, IQuestionRepository questions, CancellationToken ct)
    {
        var skills = courseSkills.ToDictionary(s => s.Id);
        var answers = (await sessions.ListByCourseAsync(kidId, course.Id, ct)).SelectMany(s => s.Answers).ToList();
        var asked = (await questions.ListByIdsAsync(answers.Select(a => a.QuestionId).Distinct().ToList(), ct))
            .ToDictionary(q => q.Id, q => StudentQuestions.CategoryOf(q, skills));

        var placed = answers
            .Where(a => asked.GetValueOrDefault(a.QuestionId) is not null)
            .Select(a => new CategoryAnswer(asked[a.QuestionId]!.Value, a.IsCorrect, a.AnsweredAt));
        var standings = CategoryProgress.From(course.Categories.Select(c => c.Number), placed);
        var counts = liveInCourse.Select(q => StudentQuestions.CategoryOf(q, skills)).Where(n => n is not null)
            .GroupBy(n => n!.Value).ToDictionary(g => g.Key, g => g.Count());
        var names = course.Categories.ToDictionary(c => c.Number, c => c.Name);

        return standings.Select(s => new CategoryView(
            s.Category, names[s.Category], s.Level, s.Answered, s.Correct, s.Accuracy, counts.GetValueOrDefault(s.Category))).ToList();
    }

    /// <summary>Live questions tagged to the course's skills.</summary>
    public static IReadOnlyList<Question> LiveIn(IEnumerable<Question> live, IReadOnlyList<Skill> courseSkills)
    {
        var ids = courseSkills.Select(s => s.Id).ToHashSet();
        return live.Where(q => q.IsVisibleToStudents && q.SkillIds.Any(ids.Contains)).ToList();
    }
}
