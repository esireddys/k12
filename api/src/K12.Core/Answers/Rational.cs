using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;

namespace K12.Core.Answers;

/// <summary>An exact fraction, so 0.1 + 0.2 style rounding never decides a kid's score.</summary>
public readonly partial record struct Rational
{
    public BigInteger Numerator { get; }
    public BigInteger Denominator { get; }

    public Rational(BigInteger numerator, BigInteger denominator)
    {
        if (denominator.IsZero) throw new DivideByZeroException();
        if (denominator.Sign < 0) { numerator = -numerator; denominator = -denominator; }
        var gcd = BigInteger.GreatestCommonDivisor(numerator, denominator);
        if (gcd > 1) { numerator /= gcd; denominator /= gcd; }
        Numerator = numerator;
        Denominator = denominator;
    }

    /// <summary>
    /// Parses "12", "-3", "0.75", ".5", "3/4" and mixed numbers like "1 1/2".
    /// Whitespace around parts is allowed; anything else fails.
    /// </summary>
    public static bool TryParse(string text, out Rational value)
    {
        value = default;
        text = text.Trim();
        if (text.Length == 0) return false;

        var mixed = MixedNumber().Match(text);
        if (mixed.Success)
        {
            var whole = BigInteger.Parse(mixed.Groups["whole"].Value, CultureInfo.InvariantCulture);
            var num = BigInteger.Parse(mixed.Groups["num"].Value, CultureInfo.InvariantCulture);
            var den = BigInteger.Parse(mixed.Groups["den"].Value, CultureInfo.InvariantCulture);
            if (den.IsZero) return false;
            var sign = mixed.Groups["sign"].Value == "-" ? -1 : 1;
            value = new Rational(sign * (whole * den + num), den);
            return true;
        }

        var fraction = SimpleFraction().Match(text);
        if (fraction.Success)
        {
            var den = BigInteger.Parse(fraction.Groups["den"].Value, CultureInfo.InvariantCulture);
            if (den.IsZero) return false;
            var num = BigInteger.Parse(fraction.Groups["num"].Value, CultureInfo.InvariantCulture);
            value = new Rational(num, den);
            return true;
        }

        var dec = DecimalNumber().Match(text);
        if (dec.Success)
        {
            var intPart = dec.Groups["int"].Value;
            var fracPart = dec.Groups["frac"].Value;
            var digits = BigInteger.Parse((intPart.Length == 0 ? "0" : intPart) + fracPart, CultureInfo.InvariantCulture);
            var scale = BigInteger.Pow(10, fracPart.Length);
            if (dec.Groups["sign"].Value == "-") digits = -digits;
            value = new Rational(digits, scale);
            return true;
        }

        return false;
    }

    public override string ToString() =>
        Denominator.IsOne ? Numerator.ToString(CultureInfo.InvariantCulture) : $"{Numerator}/{Denominator}";

    [GeneratedRegex(@"^(?<sign>-?)(?<whole>\d+)\s+(?<num>\d+)\s*/\s*(?<den>\d+)$")]
    private static partial Regex MixedNumber();

    [GeneratedRegex(@"^(?<num>-?\d+)\s*/\s*(?<den>\d+)$")]
    private static partial Regex SimpleFraction();

    [GeneratedRegex(@"^(?<sign>-?)(?<int>\d*)(?:\.(?<frac>\d+))?$")]
    private static partial Regex DecimalNumber();
}
