namespace K12.Core.Answers;

/// <summary>The kind of value a question expects as its answer.</summary>
public enum AnswerKind
{
    /// <summary>A multiple-choice letter or option id, for example "C".</summary>
    Choice,
    Integer,
    Decimal,
    Fraction,
    Money,
    /// <summary>A clock time such as "3:45".</summary>
    Time,
}

/// <summary>
/// How a question's answer is stored and checked. Stored as JSON on the question.
/// Example: { "kind": "Fraction", "value": "3/4", "acceptEquivalent": true, "accept": ["0.75"] }
/// </summary>
public sealed record AnswerSpec
{
    public required AnswerKind Kind { get; init; }

    /// <summary>The canonical correct answer.</summary>
    public required string Value { get; init; }

    /// <summary>For fractions: count equal values (2/4 for 1/2) as correct. Defaults to true.</summary>
    public bool AcceptEquivalent { get; init; } = true;

    /// <summary>For fractions: the response must be in lowest terms.</summary>
    public bool RequireSimplest { get; init; }

    /// <summary>Extra responses that also count as correct.</summary>
    public IReadOnlyList<string> Accept { get; init; } = [];
}

/// <summary>The result of checking one response.</summary>
/// <param name="IsCorrect">Whether the response matches the answer spec.</param>
/// <param name="Understood">False when the response could not be read as the expected kind.</param>
/// <param name="Normalized">The cleaned-up response, for display and logging.</param>
public sealed record CheckResult(bool IsCorrect, bool Understood, string Normalized);
