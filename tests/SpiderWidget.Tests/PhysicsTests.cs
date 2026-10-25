using System.Drawing;
using System.Numerics;
using SpiderWidget.Spider;
using SpiderWidget.Targets;

namespace SpiderWidget.Tests;

public class PhysicsTests
{
    private const float Dt = Tuning.StepSeconds;

    private static Surface Edge(int left, int top, int width) =>
        new(new Rectangle(left, top, width, 0), SurfaceKind.Floor);

    private static void Run(Physics body, IReadOnlyList<Surface> surfaces, float seconds)
    {
        for (float t = 0; t < seconds; t += Dt)
        {
            body.Step(Dt, surfaces);
        }
    }

    [Fact]
    public void ReleasedAboveEdge_EndsStandingOnIt()
    {
        var body = new Physics { Position = new Vector2(100, 0) };
        Surface[] surfaces = [Edge(0, 500, 300)];

        Run(body, surfaces, 3);

        Assert.True(body.Grounded);
        Assert.Equal(new Vector2(100, 500), body.Position);
        Assert.Equal(Vector2.Zero, body.Velocity);
    }

    [Fact]
    public void FastFall_DoesNotTunnelThroughEdge()
    {
        // 3000 px/с × 1/60 с = 50 px за шаг, а до кромки 10 px.
        var body = new Physics { Position = new Vector2(100, 490), Velocity = new Vector2(0, 3000) };
        Surface[] surfaces = [Edge(0, 500, 300)];

        body.Step(Dt, surfaces);

        Assert.True(body.Grounded);
        Assert.Equal(500, body.Position.Y);
    }

    [Fact]
    public void LandsOnHighestEdgeCrossed()
    {
        var body = new Physics { Position = new Vector2(100, 0) };
        Surface[] surfaces = [Edge(0, 800, 300), Edge(50, 400, 100)];

        Run(body, surfaces, 3);

        Assert.Equal(400, body.Position.Y);
    }

    [Fact]
    public void EdgeOutsideHorizontally_IsIgnored()
    {
        var body = new Physics { Position = new Vector2(100, 0) };
        Surface[] surfaces = [Edge(200, 400, 100), Edge(0, 800, 300)];

        Run(body, surfaces, 3);

        Assert.Equal(800, body.Position.Y);
    }

    [Fact]
    public void EdgeRemovedUnderGroundedBody_BodyFallsAgain()
    {
        var body = new Physics { Position = new Vector2(100, 0) };
        Run(body, [Edge(0, 400, 300), Edge(0, 800, 300)], 3);
        Assert.Equal(400, body.Position.Y);

        Run(body, [Edge(0, 800, 300)], 3);

        Assert.True(body.Grounded);
        Assert.Equal(800, body.Position.Y);
    }
}
