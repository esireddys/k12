using System.Text.Json;
using K12.Domain.Common;
using K12.Domain.Curriculum;

namespace K12.Domain.Content;

public readonly record struct PassageId(Guid Value)
{
    public static PassageId New() => new(Guid.NewGuid());
}

/// <summary>
/// A reading selection that several questions are about, for example a story or an article on a reading test.
/// Students read it beside its questions. Questions point at a passage by id; the passage does not list them,
/// so a passage can be shared by a released test and by generated questions written about it later.
/// </summary>
/// <remarks>
/// The content is JSON, like a question's: the printed pages as pictures (the source of truth), the text read
/// from them, and whether that text still needs checking. Its language is its course's.
/// </remarks>
public sealed class Passage : AggregateRoot<PassageId>
{
    /// <summary>For the persistence layer only.</summary>
    private Passage() : base(default)
    {
        Title = ContentJson = string.Empty;
    }

    private Passage(PassageId id, CourseId courseId, string title, string contentJson, QuestionOrigin origin) : base(id)
    {
        CourseId = courseId;
        Origin = origin;
        Title = string.Empty;
        ContentJson = "{}";
        Revise(title, contentJson);
    }

    public CourseId CourseId { get; }
    /// <summary>The title as printed, for example "The Unwelcome Neighbor".</summary>
    public string Title { get; private set; }
    public string ContentJson { get; private set; }
    public QuestionOrigin Origin { get; }

    public static Passage Create(CourseId courseId, string title, string contentJson, QuestionOrigin origin) =>
        new(PassageId.New(), courseId, title, contentJson, origin);

    /// <summary>Corrects the title or the content, for example after checking the text read from the PDF.</summary>
    public void Revise(string title, string contentJson)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new DomainException("A passage needs a title.");
        if (title.Trim().Length > MaxTitleLength) throw new DomainException($"A passage title is at most {MaxTitleLength} characters.");
        try
        {
            using var doc = JsonDocument.Parse(contentJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) throw new DomainException("The passage content must be a JSON object.");
        }
        catch (JsonException)
        {
            throw new DomainException("The passage content is not valid JSON.");
        }

        Title = title.Trim();
        ContentJson = contentJson;
    }

    public const int MaxTitleLength = 200;
}
