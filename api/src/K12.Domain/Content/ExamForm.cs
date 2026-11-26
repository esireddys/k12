using K12.Domain.Common;
using K12.Domain.Curriculum;

namespace K12.Domain.Content;

public readonly record struct ExamFormId(Guid Value)
{
    public static ExamFormId New() => new(Guid.NewGuid());
}

/// <summary>One numbered slot on a released test, pointing at the question that fills it.</summary>
public sealed record ExamFormItem(int Number, QuestionId QuestionId);

/// <summary>
/// A released test as it was given, for example the spring 2021 Grade 3 math test.
/// Students who pick "a specific year" get its items in order. The exam name is data from the import.
/// </summary>
public sealed class ExamForm : AggregateRoot<ExamFormId>
{
    private readonly List<ExamFormItem> _items = [];

    /// <summary>For the persistence layer only.</summary>
    private ExamForm() : base(default)
    {
        Exam = Season = SourceUrl = string.Empty;
    }

    private ExamForm(ExamFormId id, CourseId courseId, string exam, int year, string season, string sourceUrl) : base(id)
    {
        if (string.IsNullOrWhiteSpace(exam)) throw new DomainException("An exam form needs an exam name.");
        if (year is < 1990 or > 2100) throw new DomainException($"{year} is not a plausible test year.");
        if (string.IsNullOrWhiteSpace(season)) throw new DomainException("An exam form needs a season.");
        CourseId = courseId;
        Exam = exam.Trim();
        Year = year;
        Season = season.Trim().ToLowerInvariant();
        SourceUrl = sourceUrl.Trim();
    }

    public CourseId CourseId { get; }
    public string Exam { get; }
    public int Year { get; }
    public string Season { get; }
    public string SourceUrl { get; }
    public IReadOnlyList<ExamFormItem> Items => _items;

    public static ExamForm Create(CourseId courseId, string exam, int year, string season, string sourceUrl) =>
        new(ExamFormId.New(), courseId, exam, year, season, sourceUrl);

    /// <summary>Places a question in a numbered slot. Each number is used once.</summary>
    public void AddItem(int number, QuestionId questionId)
    {
        if (number < 1) throw new DomainException("Item numbers start at 1.");
        if (_items.Any(i => i.Number == number)) throw new DomainException($"Item {number} is already on this form.");
        if (_items.Any(i => i.QuestionId == questionId)) throw new DomainException("A question can fill only one slot on a form.");
        _items.Add(new ExamFormItem(number, questionId));
        _items.Sort((a, b) => a.Number.CompareTo(b.Number));
    }
}
