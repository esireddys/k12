using K12.Domain.Common;
using K12.Domain.Curriculum;

namespace K12.Domain.Accounts;

public readonly record struct KidProfileId(Guid Value)
{
    public static KidProfileId New() => new(Guid.NewGuid());
}

/// <summary>The colour of a kid's picture. Kids get a coloured initial, never a photo.</summary>
public enum AvatarColor { Orange, Blue, Green, Purple, Pink, Teal }

/// <summary>
/// A kid who practices under a parent's sign-in. Children's privacy: a profile holds a first name or nickname,
/// a picture colour and a course, and never an email, photo or location.
/// </summary>
public sealed class KidProfile : Entity<KidProfileId>
{
    public const int MaxNameLength = 30;

    /// <summary>For the persistence layer only.</summary>
    private KidProfile() : base(default) => Name = string.Empty;

    internal KidProfile(KidProfileId id, string name, AvatarColor avatar, CourseId? courseId) : base(id)
    {
        Name = string.Empty;
        Change(name, avatar, courseId);
    }

    public string Name { get; private set; }
    public AvatarColor Avatar { get; private set; }

    /// <summary>The course the kid practices, for example Grade 3 math. Empty until someone picks one.</summary>
    public CourseId? CourseId { get; private set; }

    internal void Change(string name, AvatarColor avatar, CourseId? courseId)
    {
        Name = ValidName(name);
        if (!Enum.IsDefined(avatar)) throw new DomainException("Pick one of the picture colours.");
        Avatar = avatar;
        CourseId = courseId;
    }

    internal void ChooseCourse(CourseId courseId) => CourseId = courseId;

    private static string ValidName(string name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length == 0) throw new DomainException("A kid needs a name.");
        if (trimmed.Length > MaxNameLength) throw new DomainException($"Keep the name to {MaxNameLength} characters.");
        // A first name or nickname only: nothing that could be an email address or a link.
        if (trimmed.Contains('@') || trimmed.Contains("://") || trimmed.Contains('/'))
            throw new DomainException("Use a first name or nickname, not an email or a link.");
        return trimmed;
    }
}
