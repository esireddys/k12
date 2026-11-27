using K12.Domain.Common;

namespace K12.Domain.Curriculum;

public readonly record struct CourseId(Guid Value)
{
    public static CourseId New() => new(Guid.NewGuid());
}

/// <summary>
/// Whether a course covers a whole grade's subject (Grade 3 math) or is a course students take in some grade,
/// such as Algebra I. It decides how the course is named and grouped for students.
/// </summary>
public enum CourseKind { Grade, Course }

/// <summary>A group of skills that a test reports on, numbered within its course (for example 1 to 4).</summary>
public sealed record ReportingCategory(int Number, string Name);

/// <summary>
/// One subject at one level in one jurisdiction, for example Texas Grade 3 math or Texas Algebra I.
/// Students pick a course before they practice. The framework (such as a state's standards) is data, never code.
/// </summary>
public sealed class Course : AggregateRoot<CourseId>
{
    private readonly List<ReportingCategory> _categories = [];

    /// <summary>For the persistence layer only.</summary>
    private Course() : base(default)
    {
        Jurisdiction = Subject = Code = Name = FrameworkCode = FrameworkName = JurisdictionName = string.Empty;
    }

    private Course(CourseId id, string jurisdiction, string subject, string code) : base(id)
    {
        Jurisdiction = Required(jurisdiction, "jurisdiction").ToUpperInvariant();
        Subject = Required(subject, "subject").ToLowerInvariant();
        Code = Required(code, "code").ToUpperInvariant();
        Name = FrameworkCode = FrameworkName = JurisdictionName = string.Empty;
    }

    /// <summary>A two-letter state or country code, for example "TX".</summary>
    public string Jurisdiction { get; }
    /// <summary>The jurisdiction's display name from curriculum data, for example "Texas". Empty until an import names it.</summary>
    public string JurisdictionName { get; private set; }

    public string Subject { get; }
    /// <summary>The level code within the jurisdiction, for example "G3" or "A1".</summary>
    public string Code { get; }
    public string Name { get; private set; }
    public string FrameworkCode { get; private set; }
    public string FrameworkName { get; private set; }
    public IReadOnlyList<ReportingCategory> Categories => _categories;

    /// <summary>A whole grade or a course such as Algebra I. Grade until an import says otherwise.</summary>
    public CourseKind Kind { get; private set; }

    /// <summary>
    /// The grade students usually are in when they take this course, 0 for kindergarten to 12. Lets the app list
    /// Grade 3 before Algebra I (usually grade 9). Empty until an import says.
    /// </summary>
    public int? TypicalGrade { get; private set; }

    public const int Kindergarten = 0, LastGrade = 12;

    public static Course Create(
        string jurisdiction, string subject, string code, string name,
        string frameworkCode, string frameworkName, IEnumerable<ReportingCategory> categories)
    {
        var course = new Course(CourseId.New(), jurisdiction, subject, code);
        course.Describe(name, frameworkCode, frameworkName, categories);
        return course;
    }

    /// <summary>Replaces the name, framework and reporting categories, for example after re-importing curriculum data.</summary>
    public void Describe(string name, string frameworkCode, string frameworkName, IEnumerable<ReportingCategory> categories)
    {
        var list = categories.ToList();
        if (list.Select(c => c.Number).Distinct().Count() != list.Count)
            throw new DomainException("Reporting category numbers must be unique within a course.");
        if (list.Any(c => string.IsNullOrWhiteSpace(c.Name)))
            throw new DomainException("Every reporting category needs a name.");

        Name = Required(name, "name");
        FrameworkCode = Required(frameworkCode, "framework code");
        FrameworkName = Required(frameworkName, "framework name");
        _categories.Clear();
        _categories.AddRange(list.OrderBy(c => c.Number));
    }

    /// <summary>Sets the name students see for the jurisdiction. Comes from curriculum data, never from code.</summary>
    public void NameJurisdiction(string? name) => JurisdictionName = (name ?? string.Empty).Trim();

    /// <summary>Says whether this is a whole grade or a course, and the grade students usually take it in.</summary>
    public void Place(CourseKind kind, int? typicalGrade)
    {
        if (!Enum.IsDefined(kind)) throw new DomainException("A course is either a grade or a course.");
        if (typicalGrade is < Kindergarten or > LastGrade)
            throw new DomainException($"A typical grade runs from {Kindergarten} (kindergarten) to {LastGrade}.");
        if (kind == CourseKind.Grade && typicalGrade is null)
            throw new DomainException("A grade-level course needs its grade.");
        Kind = kind;
        TypicalGrade = typicalGrade;
    }

    public bool HasCategory(int number) => _categories.Any(c => c.Number == number);

    private static string Required(string value, string what) =>
        string.IsNullOrWhiteSpace(value) ? throw new DomainException($"A course needs a {what}.") : value.Trim();
}
