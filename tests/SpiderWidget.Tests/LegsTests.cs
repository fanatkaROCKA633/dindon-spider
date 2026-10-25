using System.Numerics;
using SpiderWidget.Spider;

namespace SpiderWidget.Tests;

public class LegsTests
{
    [Theory]
    [InlineData(1, 6, 8)]
    [InlineData(-1, -6, 8)]
    [InlineData(1, 2, 7)]
    [InlineData(-1, -12, 6)]
    public void Ik_ReachableTarget_EndOnTarget_SegmentsKeepLength(int side, float x, float y)
    {
        var hip = new Vector2(0, 0);
        var target = new Vector2(x, y);

        (Vector2 knee, Vector2 end) = Legs.SolveIk(hip, target, 7, 7, side);

        Assert.True(Vector2.Distance(end, target) <= 0.5f, $"end {end} target {target}");
        Assert.Equal(7, Vector2.Distance(hip, knee), 3);
        Assert.Equal(7, Vector2.Distance(knee, end), 3);
    }

    [Fact]
    public void Ik_UnreachableTarget_LegStretchesTowardIt()
    {
        var hip = new Vector2(10, 10);
        var target = new Vector2(40, 50); // 50 px при длине ноги 14

        (Vector2 knee, Vector2 end) = Legs.SolveIk(hip, target, 7, 7, 1);

        Vector2 direction = Vector2.Normalize(target - hip);
        Assert.True(Vector2.Distance(end, hip + direction * 14) < 1e-3f);
        Assert.True(Vector2.Distance(knee, hip + direction * 7) < 1e-3f);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void Ik_KneeBendsOutwardAndUp(int side)
    {
        var hip = new Vector2(0, 0);
        var target = new Vector2(side * 6, 8);

        (Vector2 knee, _) = Legs.SolveIk(hip, target, 7, 7, side);

        Vector2 middle = (hip + target) / 2;
        Assert.True(knee.Y < middle.Y, "колено выше середины ноги");
        Assert.True(side * (knee.X - middle.X) > 0, "колено наружу");
    }

    [Fact]
    public void AtRest_SixFeetOnEdge_ThreePerSide()
    {
        var legs = new Legs(3);
        var ground = new Vector2(500, 1020);

        legs.Place(ground);
        LegPose[] poses = legs.Poses(ground).ToArray();

        Assert.Equal(6, poses.Length);
        Assert.Equal(3, legs.All.Count(l => l.Side < 0));
        Assert.All(poses, p => Assert.InRange(p.Foot.Y, ground.Y - 1, ground.Y + 1));
    }
}
