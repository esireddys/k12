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

public class CategoryMasteryTests
{
    [Theory]
    [InlineData("", MasteryLevel.NotStarted)]
    [InlineData("11111", MasteryLevel.Practicing)] // a skill would call this mastered; a category needs 8 answers
    [InlineData("1111111", MasteryLevel.Practicing)]
    [InlineData("11111111", MasteryLevel.Mastered)]
    [InlineData("11110111", MasteryLevel.Mastered)] // 7 of the last 8
    [InlineData("11100111", MasteryLevel.Practicing)] // 6 of the last 8
    [InlineData("000011111111", MasteryLevel.Mastered)] // a slow start does not hold a kid back
    [InlineData("00010", MasteryLevel.Learning)]
    public void A_category_needs_more_answers_than_a_skill(string recent, MasteryLevel expected) =>
        Assert.Equal(expected, Mastery.Level(recent, Mastery.Category));

    private static readonly DateTimeOffset T0 = new(2026, 11, 27, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Standings_cover_every_category_in_order_and_use_answers_oldest_first()
    {
        var answers = new List<CategoryAnswer>();
        // Category 2: one early miss, then nine right, given out of order.
        for (var i = 9; i >= 1; i--) answers.Add(new CategoryAnswer(2, true, T0.AddMinutes(i)));
        answers.Add(new CategoryAnswer(2, false, T0));
        answers.Add(new CategoryAnswer(3, false, T0));

        var standings = CategoryProgress.From([3, 1, 2], answers);

        Assert.Equal([1, 2, 3], standings.Select(s => s.Category));
        Assert.Equal((MasteryLevel.NotStarted, 0, (int?)null), (standings[0].Level, standings[0].Answered, standings[0].Accuracy));
        Assert.Equal((MasteryLevel.Mastered, 10, 9, (int?)90), (standings[1].Level, standings[1].Answered, standings[1].Correct, standings[1].Accuracy));
        Assert.Equal("0111111111", standings[1].Recent);
        Assert.Equal(MasteryLevel.Learning, standings[2].Level);
    }

    [Fact]
    public void The_window_keeps_only_the_latest_answers() =>
        Assert.Equal(12, CategoryProgress.From([1], Enumerable.Range(0, 30).Select(i => new CategoryAnswer(1, i % 2 == 0, T0.AddMinutes(i))))[0].Recent.Length);
}
