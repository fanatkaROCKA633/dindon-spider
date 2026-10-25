using System.Drawing;
using System.Numerics;
using SpiderWidget.Recycle;
using SpiderWidget.Spider;
using SpiderWidget.Targets;

namespace SpiderWidget.Tests;

public class EatTests
{
    private const float Dt = Tuning.StepSeconds;

    private static readonly Surface[] Floor = [new(new Rectangle(0, 1000, 2000, 0), SurfaceKind.Floor)];

    private static (SpiderBrain Brain, Physics Body) Standing()
    {
        var brain = new SpiderBrain(new Random(1));
        var body = new Physics();
        body.PlaceOn(new Vector2(1000, 1000));
        return (brain, body);
    }

    private static void Run(SpiderBrain brain, Physics body, float seconds, Action? eachStep = null)
    {
        for (float t = 0; t < seconds; t += Dt)
        {
            brain.Step(Dt, body, Floor);
            body.Step(Dt, Floor);
            eachStep?.Invoke();
        }
    }

    [Fact]
    public void Gaping_MouthOpenAndStandsStill()
    {
        (SpiderBrain brain, Physics body) = Standing();
        brain.Gape(true);

        Run(brain, body, 10, () =>
        {
            Assert.True(brain.Hold.MouthOpen);
            Assert.Equal(1000, body.Position.X);
        });
    }

    [Fact]
    public void Fed_ChewsThenGoesBackToNormal()
    {
        (SpiderBrain brain, Physics body) = Standing();
        brain.Gape(true);
        Run(brain, body, 0.5f);
        brain.Fed(ateSomething: true);
        bool opened = false;
        bool closed = false;

        Run(brain, body, Tuning.EatSeconds, () =>
        {
            opened |= brain.Hold.MouthOpen;
            closed |= !brain.Hold.MouthOpen;
        });
        Run(brain, body, 0.1f);

        Assert.True(opened && closed, "жуя, рот должен и открываться, и закрываться");
        Assert.False(brain.Busy);
        Assert.False(brain.Hold.MouthOpen);
    }

    [Fact]
    public void FedNothing_ShakesHeadThenStops()
    {
        (SpiderBrain brain, Physics body) = Standing();
        brain.Fed(ateSomething: false);
        var shifts = new HashSet<int>();

        Run(brain, body, Tuning.RefuseSeconds, () => shifts.Add(brain.Hold.HeadShift));
        Run(brain, body, 0.1f);

        Assert.Contains(shifts, s => s < 0);
        Assert.Contains(shifts, s => s > 0);
        Assert.Equal(0, brain.Hold.HeadShift);
        Assert.False(brain.Busy);
    }

    [Fact]
    public void Check_AllowedPath_IsReturnedNormalized()
    {
        var rules = new PathRules([@"C:\Windows"], [@"C:\Users\me"], _ => DriveType.Fixed, p => p);

        PathCheck check = RecycleBin.Check(@"c:\users\me\Desktop\folder\..\b.txt\", rules);

        Assert.True(check.Allowed);
        Assert.Equal(@"c:\users\me\Desktop\b.txt", check.Normalized);
    }
}
