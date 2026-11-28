namespace K12.Domain.Content.Review;

public enum OwnerOutcome { Approved, ApprovedWithEdits, Rejected }

/// <summary>The app owner's final call on a question, with who made it and when. Owners are app owners, never parents.</summary>
public sealed record OwnerDecision(OwnerOutcome Outcome, string DecidedBy, DateTimeOffset DecidedAt, string? Note);

/// <summary>What an owner can change while approving: the text students see, the answer key and the difficulty.</summary>
public sealed record OwnerEdits(string ContentJson, Answers.AnswerSpec Answer, Difficulty Difficulty);
