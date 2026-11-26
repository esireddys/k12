using K12.Core.Answers;

namespace K12.Core.Tests;

public class AnswerCheckerTests
{
    private static AnswerSpec Spec(AnswerKind kind, string value, bool equivalent = true, bool simplest = false, params string[] accept) =>
        new() { Kind = kind, Value = value, AcceptEquivalent = equivalent, RequireSimplest = simplest, Accept = accept };

    [Theory]
    [InlineData("144", true)]
    [InlineData(" 144 ", true)]
    [InlineData("144 students", true)]
    [InlineData("143", false)]
    [InlineData("", false)]
    public void Integer_answers(string response, bool expected) =>
        Assert.Equal(expected, AnswerChecker.Check(Spec(AnswerKind.Integer, "144"), response).IsCorrect);

    [Theory]
    [InlineData("1,000", true)]
    [InlineData("1000", true)]
    [InlineData("1000.0", true)]
    public void Thousands_separators_are_ignored(string response, bool expected) =>
        Assert.Equal(expected, AnswerChecker.Check(Spec(AnswerKind.Integer, "1000"), response).IsCorrect);

    [Theory]
    [InlineData("3/4", true)]
    [InlineData("6/8", true)]
    [InlineData("0.75", true)]
    [InlineData("3 / 4", true)]
    [InlineData("4/3", false)]
    [InlineData("3/0", false)]
    public void Fractions_accept_equivalent_values_by_default(string response, bool expected) =>
        Assert.Equal(expected, AnswerChecker.Check(Spec(AnswerKind.Fraction, "3/4"), response).IsCorrect);

    [Theory]
    [InlineData("1/2", true)]
    [InlineData("2/4", false)]
    public void Fractions_can_require_simplest_form(string response, bool expected) =>
        Assert.Equal(expected, AnswerChecker.Check(Spec(AnswerKind.Fraction, "1/2", simplest: true), response).IsCorrect);

    [Theory]
    [InlineData("2/4", true)]
    [InlineData("1/2", false)]
    public void Fractions_can_require_the_exact_written_form(string response, bool expected) =>
        Assert.Equal(expected, AnswerChecker.Check(Spec(AnswerKind.Fraction, "2/4", equivalent: false), response).IsCorrect);

    [Fact]
    public void Mixed_numbers_are_read() =>
        Assert.True(AnswerChecker.Check(Spec(AnswerKind.Fraction, "3/2"), "1 1/2").IsCorrect);

    [Theory]
    [InlineData("$1.50", true)]
    [InlineData("1.5", true)]
    [InlineData("1.50 dollars", true)]
    [InlineData("$1.05", false)]
    public void Money_answers(string response, bool expected) =>
        Assert.Equal(expected, AnswerChecker.Check(Spec(AnswerKind.Money, "1.50"), response).IsCorrect);

    [Theory]
    [InlineData("3:45", true)]
    [InlineData("3:45 p.m.", true)]
    [InlineData("03:45", true)]
    [InlineData("3:54", false)]
    [InlineData("345", false)]
    public void Time_answers(string response, bool expected) =>
        Assert.Equal(expected, AnswerChecker.Check(Spec(AnswerKind.Time, "3:45"), response).IsCorrect);

    [Theory]
    [InlineData("C", true)]
    [InlineData("c", true)]
    [InlineData("C)", true)]
    [InlineData("B", false)]
    public void Choice_answers(string response, bool expected) =>
        Assert.Equal(expected, AnswerChecker.Check(Spec(AnswerKind.Choice, "C"), response).IsCorrect);

    [Fact]
    public void Extra_accepted_values_count() =>
        Assert.True(AnswerChecker.Check(Spec(AnswerKind.Decimal, "0.5", accept: "1/2"), "1/2").IsCorrect);

    [Fact]
    public void Unreadable_responses_are_flagged_not_just_wrong()
    {
        var result = AnswerChecker.Check(Spec(AnswerKind.Integer, "12"), "twelve-ish?");
        Assert.False(result.IsCorrect);
        Assert.False(result.Understood);
    }
}
