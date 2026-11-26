namespace K12.Domain.Content;

public enum QuestionOrigin { Official, Generated }

public enum QuestionStatus { Draft, InAiReview, AwaitingOwner, Live, Rejected, Retired }

public enum Difficulty { Easier, OnLevel, Harder }
