using K12.Domain.Common;

namespace K12.Domain.Curriculum;

/// <summary>How many items of one reporting category a full practice test holds.</summary>
public sealed record CategoryShare(int Category, int Items);

/// <summary>
/// The shape of the real statewide test for a course, so the app can build a full-length practice test like it:
/// how many items, how long students get, and roughly how many items come from each reporting category.
/// All of it is curriculum data (from the published test blueprint or the released forms), never code.
/// </summary>
/// <param name="ItemCount">Items on the test, for example 32.</param>
/// <param name="Minutes">The time limit, or empty when the test is untimed.</param>
/// <param name="Mix">Items per reporting category. May add up to less than <paramref name="ItemCount"/>; the rest come from any category.</param>
public sealed record PracticeTestBlueprint(int ItemCount, int? Minutes, IReadOnlyList<CategoryShare> Mix)
{
    public const int MaxItems = 100;
    public const int MaxMinutes = 600;

    /// <summary>Throws unless the blueprint makes sense for <paramref name="course"/>.</summary>
    public void EnsureValidFor(Course course)
    {
        if (ItemCount is < 1 or > MaxItems) throw new DomainException($"A practice test has 1 to {MaxItems} items.");
        if (Minutes is < 1 or > MaxMinutes) throw new DomainException($"A practice test lasts 1 to {MaxMinutes} minutes, or is untimed.");
        if (Mix.Select(m => m.Category).Distinct().Count() != Mix.Count)
            throw new DomainException("Each reporting category appears once in a practice test's mix.");
        if (Mix.Any(m => m.Items < 0)) throw new DomainException("A category cannot have a negative number of items.");
        if (Mix.FirstOrDefault(m => !course.HasCategory(m.Category)) is { } unknown)
            throw new DomainException($"The practice test names reporting category {unknown.Category}, which the course does not have.");
        if (Mix.Sum(m => m.Items) > ItemCount)
            throw new DomainException("The practice test's mix adds up to more items than the test has.");
    }
}
