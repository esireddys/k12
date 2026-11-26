using K12.Domain.Common;

namespace K12.Domain.Content.Review;

/// <summary>The AI reviewer's overall call. It never rejects: it either finds nothing to fix or asks an owner to look closely.</summary>
public enum AiVerdict { LooksGood, NeedsAttention }

public enum FindingSeverity { Info, Warning, Problem }

/// <summary>One thing the AI reviewer noticed, for example "choice G reads 1/4 in the picture but 14 in the text".</summary>
/// <param name="Area">What it is about: answer_key, stem, choices, figure, skill, difficulty or other.</param>
public sealed record AiFinding(FindingSeverity Severity, string Area, string Message);

/// <summary>
/// What the AI reviewer recorded for one version of a question. A value object: replaced whole, never edited.
/// The proposed content is a suggestion; nothing in it reaches students until an owner approves it.
/// </summary>
public sealed record AiReview
{
    public required string Reviewer { get; init; }
    public required DateTimeOffset ReviewedAt { get; init; }
    public required AiVerdict Verdict { get; init; }

    /// <summary>Whether the answer the reviewer worked out matches the published key.</summary>
    public required bool AnswerKeyAgrees { get; init; }

    /// <summary>The answer the reviewer got by solving the item from its picture.</summary>
    public required string SolvedAnswer { get; init; }

    public required Difficulty SuggestedDifficulty { get; init; }

    /// <summary>The question content with cleaned-up text, as JSON in the same shape as the question's content.</summary>
    public required string ProposedContentJson { get; init; }

    public required string Summary { get; init; }
    public IReadOnlyList<AiFinding> Findings { get; init; } = [];

    /// <summary>Checks the parts every review must have. Called by the question before it accepts a review.</summary>
    public void EnsureComplete()
    {
        if (string.IsNullOrWhiteSpace(Reviewer)) throw new DomainException("An AI review must name the reviewer.");
        if (string.IsNullOrWhiteSpace(ProposedContentJson)) throw new DomainException("An AI review must include the proposed content.");
        if (string.IsNullOrWhiteSpace(Summary)) throw new DomainException("An AI review must include a summary.");
        if (!AnswerKeyAgrees && Verdict == AiVerdict.LooksGood)
            throw new DomainException("A review that disagrees with the answer key cannot say the question looks good.");
        if (Findings.Any(f => f.Severity == FindingSeverity.Problem) && Verdict == AiVerdict.LooksGood)
            throw new DomainException("A review with a problem finding cannot say the question looks good.");
    }
}
