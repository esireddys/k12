using K12.Domain.Progress;

namespace K12.Domain.Tests.Progress;

public class MasteryTests
{
    [Theory]
    [InlineData("", MasteryLevel.NotStarted)]
    [InlineData("0", MasteryLevel.Learning)]
    [InlineData("1", MasteryLevel.Practicing)]
    [InlineData("10", MasteryLevel.Practicing)] // half right is not "learning"
    [InlineData("100", MasteryLevel.Learning)]
    [InlineData("1111", MasteryLevel.Practicing)] // right every time, but too few answers to call it mastered
    [InlineData("11111", MasteryLevel.Mastered)]
    [InlineData("11011", MasteryLevel.Mastered)] // 4 of the last 5
    [InlineData("10011", MasteryLevel.Practicing)] // 3 of the last 5
    [InlineData("0000011110", MasteryLevel.Mastered)] // a slow start does not hold a kid back
    [InlineData("1111100100", MasteryLevel.Practicing)] // slipping lately: no longer mastered
    public void Level_comes_from_recent_answers(string recent, MasteryLevel expected) =>
        Assert.Equal(expected, Mastery.Level(recent));

    [Theory]
    [InlineData("", true, "1")]
    [InlineData("10", false, "100")]
    [InlineData("1111111111", false, "1111111110")] // the window keeps the latest 10
    [InlineData("0101010101", true, "1010101011")]
    public void Push_adds_the_answer_and_keeps_the_window(string recent, bool correct, string expected) =>
        Assert.Equal(expected, Mastery.Push(recent, correct));
}
