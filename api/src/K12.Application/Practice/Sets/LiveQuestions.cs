using K12.Domain.Content;
using K12.Domain.Curriculum;

namespace K12.Application.Practice.Sets;

/// <summary>
/// The Live questions of one course: those tagged to any of its skills. Only Live questions ever reach students.
/// Loads every Live question and filters in memory, which is fine for a few thousand; a read model replaces it
/// when the bank grows past that.
/// </summary>
internal static class LiveQuestions
{
    public static async Task<IReadOnlyList<Question>> ForSkillsAsync(
        IQuestionRepository questions, IReadOnlyCollection<SkillId> skillIds, CancellationToken ct)
    {
        var wanted = skillIds.ToHashSet();
        var live = await questions.ListByStatusAsync(QuestionStatus.Live, int.MaxValue, ct);
        return live.Where(q => q.IsVisibleToStudents && q.SkillIds.Any(wanted.Contains)).ToList();
    }
}
