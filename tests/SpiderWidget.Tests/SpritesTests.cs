using System.Numerics;
using SpiderWidget.Overlay;
using SpiderWidget.Spider;

namespace SpiderWidget.Tests;

public class SpritesTests
{
    public static TheoryData<string> HeadIds()
    {
        var ids = new TheoryData<string>();
        foreach (HeadStyle head in Sprites.Heads)
        {
            ids.Add(head.Id);
        }

        return ids;
    }

    private static HeadStyle Head(string id) => Sprites.Heads.Single(h => h.Id == id);

    [Theory]
    [MemberData(nameof(HeadIds))]
    public void Head_RowsSameWidth_AllPixelsInPalette(string id)
    {
        foreach (Sprite sprite in new[] { Head(id).Closed, Head(id).Open })
        {
            Assert.All(sprite.Rows, row => Assert.Equal(sprite.Width, row.Length));
            Assert.All(sprite.Rows.SelectMany(r => r), ch => Assert.True(ch == '.' || sprite.Palette.ContainsKey(ch), $"нет цвета для '{ch}'"));
        }
    }

    [Theory]
    [MemberData(nameof(HeadIds))]
    public void Head_MouthOpenIsSameSizeButDifferent(string id)
    {
        HeadStyle head = Head(id);

        Assert.Equal(head.Closed.Width, head.Open.Width);
        Assert.Equal(head.Closed.Height, head.Open.Height);
        Assert.NotEqual(head.Closed.Rows, head.Open.Rows);
    }

    // Окно паука 48×44 логических пикселя; голова со всеми ногами должна в него влезать.
    [Theory]
    [MemberData(nameof(HeadIds))]
    public void Head_FitsSpiderWindowWithLegs(string id)
    {
        Sprite head = Head(id).Closed;
        var legs = new Legs(1, head.Height);

        float headTop = legs.BodyHeight + head.Height / 2f + 1; // +1 — покачивание при шаге
        Assert.True(headTop <= 44, $"верх головы на {headTop} над опорой");
        Assert.True(head.Width <= 48);
    }

    [Fact]
    public void Heads_HaveExpectedNamesAndUniqueIds()
    {
        Assert.Equal(["Диндон", "Лёвчик", "Илюха"], Sprites.Heads.Select(h => h.Name)); // Диндон — первая и по умолчанию
        Assert.Equal(Sprites.Heads.Count, Sprites.Heads.Select(h => h.Id).Distinct().Count());
    }

    [Theory]
    [InlineData(38)]
    [InlineData(34)]
    [InlineData(32)]
    public void Legs_ForAnyHeadHeight_FeetOnEdgeAndHipsUnderHead(int headHeight)
    {
        var legs = new Legs(3, headHeight);
        var ground = new Vector2(500, 1000);
        legs.Place(ground);

        Vector2 center = legs.BodyCenter(ground);
        float headBottom = center.Y + headHeight / 2f * 3;
        foreach (LegPose pose in legs.Poses(ground))
        {
            Assert.InRange(pose.Foot.Y, ground.Y - 1, ground.Y + 1);
            Assert.True(pose.Hip.Y < headBottom, "плечо должно быть под головой, а не ниже неё");
        }
    }
}
