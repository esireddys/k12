using K12.Domain.Content;
using K12.Domain.Content.Review;

namespace K12.Infrastructure.Tests;

/// <summary>A complete AI review for tests that only need a question to reach the owners.</summary>
internal static class TestReviews
{
    public static AiReview LooksGood(string proposedContentJson = "{}", params AiFinding[] findings) => new()
    {
        Reviewer = "test-reviewer",
        ReviewedAt = DateTimeOffset.UnixEpoch,
        Verdict = AiVerdict.LooksGood,
        AnswerKeyAgrees = true,
        SolvedAnswer = "x",
        SuggestedDifficulty = Difficulty.OnLevel,
        ProposedContentJson = proposedContentJson,
        Summary = "Matches the picture.",
        Findings = findings,
    };
}
