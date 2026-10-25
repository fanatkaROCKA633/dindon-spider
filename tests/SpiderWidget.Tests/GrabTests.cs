using System.Drawing;
using System.Numerics;
using SpiderWidget.Overlay;
using SpiderWidget.Spider;
using SpiderWidget.Targets;

namespace SpiderWidget.Tests;

public class GrabTests
{
    private const float Dt = Tuning.StepSeconds;

    private static Surface Floor(int left, int top, int width) =>
        new(new Rectangle(left, top, width, 0), SurfaceKind.Floor);

    [Fact]
    public void ThrowVelocity_IsCursorSpeedOverLastSamples()
    {
        var grab = new Grab();
        for (int i = 0; i <= 3; i++)
        {
            grab.Add(new Vector2(100 + i * 30, 500 - i * 10), i / 30f); // 900 px/с вправо, 300 px/с вверх
        }

        Vector2 velocity = grab.ThrowVelocity();

        Assert.Equal(900, velocity.X, 1);
        Assert.Equal(-300, velocity.Y, 1);
    }

    [Fact]
    public void ThrowVelocity_IsCappedAtMaxThrowSpeed()
    {
        var grab = new Grab();
        grab.Add(new Vector2(0, 0), 0);
        grab.Add(new Vector2(5000, 0), 0.05f); // 100 000 px/с

        Assert.Equal(Tuning.MaxThrowSpeed, grab.ThrowVelocity().Length(), 1);
    }

    [Fact]
    public void ThrowVelocity_HeldStillBeforeRelease_IsZero()
    {
        var grab = new Grab();
        grab.Add(new Vector2(0, 0), 0);
        grab.Add(new Vector2(400, 0), 0.05f); // рывок
        for (float t = 0.1f; t <= 0.6f; t += 1 / 30f)
        {
            grab.Add(new Vector2(400, 0), t); // потом полсекунды держал на месте
        }

        Assert.Equal(Vector2.Zero, grab.ThrowVelocity());
    }

    [Fact]
    public void ThrownRight_LandsToTheRight()
    {
        Surface[] surfaces = [Floor(0, 1000, 3000)];
        var brain = new SpiderBrain(new Random(1));
        var body = new Physics { Position = new Vector2(500, 600) };
        brain.Grab(body);

        brain.Release(body, new Vector2(800, -200));
        for (float t = 0; t < 5 && !body.Grounded; t += Dt)
        {
            brain.Step(Dt, body, surfaces);
            body.Step(Dt, surfaces);
        }

        Assert.True(body.Grounded);
        Assert.Equal(1000, body.Position.Y);
        Assert.True(body.Position.X > 800, $"приземлился на x={body.Position.X}");
    }

    [Fact]
    public void ThrownHardAtScreenEdge_StaysOnScreen()
    {
        Surface[] surfaces = [Floor(0, 1000, 2000)];
        var brain = new SpiderBrain(new Random(1));
        var body = new Physics { Position = new Vector2(300, 600), World = new RectangleF(54, 0, 1892, 1000) };
        brain.Grab(body);

        brain.Release(body, new Vector2(-Tuning.MaxThrowSpeed, 0));
        for (float t = 0; t < 5 && !body.Grounded; t += Dt)
        {
            brain.Step(Dt, body, surfaces);
            body.Step(Dt, surfaces);
        }

        Assert.True(body.Grounded);
        Assert.Equal(54, body.Position.X);
    }

    [Fact]
    public void ClickWithoutMove_OnGround_StaysWhereItWas()
    {
        Surface[] surfaces = [Floor(0, 1000, 2000)];
        var brain = new SpiderBrain(new Random(1));
        var body = new Physics();
        body.PlaceOn(new Vector2(500, 1000));
        brain.Grab(body);

        brain.Release(body, Vector2.Zero);
        body.Step(Dt, surfaces);

        Assert.True(body.Grounded);
        Assert.Equal(new Vector2(500, 1000), body.Position);
    }
}
