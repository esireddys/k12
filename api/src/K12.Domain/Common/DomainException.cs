namespace K12.Domain.Common;

/// <summary>A business rule was broken, for example approving a question that is not awaiting approval.</summary>
public sealed class DomainException(string message) : Exception(message);
