using System.Numerics;
using System.Text.RegularExpressions;

namespace K12.Domain.Content.Answers;

/// <summary>
/// Scores a student's response against a question's <see cref="AnswerSpec"/>.
/// Deterministic and exact: no AI is involved while a student is answering.
/// </summary>
public static partial class AnswerChecker
{
    public static CheckResult Check(AnswerSpec spec, string? response)
    {
        var raw = response ?? string.Empty;

        if (spec.Kind == AnswerKind.Choice)
        {
            var choice = raw.Trim().TrimEnd('.', ')').ToUpperInvariant();
            var ok = Same(choice, spec.Value) || spec.Accept.Any(a => Same(choice, a));
            return new CheckResult(ok, choice.Length > 0, choice);
        }

        if (spec.Kind == AnswerKind.Time)
        {
            var cleaned = CleanTime(raw);
            if (!TryParseTime(cleaned, out var minutes)) return new CheckResult(false, false, cleaned);
            var ok = (TryParseTime(CleanTime(spec.Value), out var expected) && minutes == expected)
                     || spec.Accept.Any(a => TryParseTime(CleanTime(a), out var alt) && alt == minutes);
            return new CheckResult(ok, true, cleaned);
        }

        var normalized = CleanNumber(raw);
        if (!Rational.TryParse(normalized, out var value)) return new CheckResult(false, false, normalized);

        if (spec.Accept.Any(a => Rational.TryParse(CleanNumber(a), out var alt) && alt == value))
            return new CheckResult(true, true, normalized);

        if (!Rational.TryParse(CleanNumber(spec.Value), out var answer))
            throw new InvalidOperationException($"Answer spec value '{spec.Value}' is not a valid {spec.Kind}.");

        var correct = spec.Kind switch
        {
            AnswerKind.Fraction when !spec.AcceptEquivalent => SameWrittenForm(normalized, CleanNumber(spec.Value)),
            AnswerKind.Fraction when spec.RequireSimplest => value == answer && IsWrittenInLowestTerms(normalized),
            _ => value == answer,
        };
        return new CheckResult(correct, true, normalized);
    }

    private static bool Same(string a, string b) =>
        string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>Drops commas, currency signs and unit words: "$1,000 dollars" becomes "1000".</summary>
    private static string CleanNumber(string text)
    {
        var s = text.ToLowerInvariant().Replace(",", "").Replace("$", "").Replace("¢", "");
        s = Letters().Replace(s, " ");
        return Spaces().Replace(s, " ").Trim();
    }

    private static string CleanTime(string text)
    {
        var s = Letters().Replace(text.ToLowerInvariant().Replace(".", ""), "");
        return Spaces().Replace(s, "").Trim();
    }

    private static bool TryParseTime(string text, out int minutes)
    {
        minutes = 0;
        var m = ClockTime().Match(text);
        if (!m.Success) return false;
        var h = int.Parse(m.Groups["h"].Value);
        var min = int.Parse(m.Groups["m"].Value);
        if (h > 23 || min > 59) return false;
        minutes = h * 60 + min;
        return true;
    }

    private static bool SameWrittenForm(string response, string expected) =>
        Spaces().Replace(response, "") == Spaces().Replace(expected, "");

    private static bool IsWrittenInLowestTerms(string response)
    {
        var m = WrittenFraction().Match(response);
        if (!m.Success) return !response.Contains('.');
        var num = BigInteger.Parse(m.Groups["num"].Value);
        var den = BigInteger.Parse(m.Groups["den"].Value);
        return BigInteger.GreatestCommonDivisor(num, den).IsOne;
    }

    [GeneratedRegex(@"[a-z]+")]
    private static partial Regex Letters();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    [GeneratedRegex(@"^(?<h>\d{1,2}):(?<m>\d{2})$")]
    private static partial Regex ClockTime();

    [GeneratedRegex(@"(?<num>\d+)\s*/\s*(?<den>\d+)$")]
    private static partial Regex WrittenFraction();
}
