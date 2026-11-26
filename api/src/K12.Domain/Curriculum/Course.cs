using K12.Domain.Common;

namespace K12.Domain.Curriculum;

public readonly record struct CourseId(Guid Value)
{
    public static CourseId New() => new(Guid.NewGuid());
}

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
        Jurisdiction = Subject = Code = Name = FrameworkCode = FrameworkName = string.Empty;
    }

    private Course(CourseId id, string jurisdiction, string subject, string code) : base(id)
    {
        Jurisdiction = Required(jurisdiction, "jurisdiction").ToUpperInvariant();
        Subject = Required(subject, "subject").ToLowerInvariant();
        Code = Required(code, "code").ToUpperInvariant();
        Name = FrameworkCode = FrameworkName = string.Empty;
    }

    /// <summary>A two-letter state or country code, for example "TX".</summary>
    public string Jurisdiction { get; }
    public string Subject { get; }
    /// <summary>The level code within the jurisdiction, for example "G3" or "A1".</summary>
    public string Code { get; }
    public string Name { get; private set; }
    public string FrameworkCode { get; private set; }
    public string FrameworkName { get; private set; }
    public IReadOnlyList<ReportingCategory> Categories => _categories;

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

    public bool HasCategory(int number) => _categories.Any(c => c.Number == number);

    private static string Required(string value, string what) =>
        string.IsNullOrWhiteSpace(value) ? throw new DomainException($"A course needs a {what}.") : value.Trim();
}
