using System.Drawing;
using System.Numerics;
using SpiderWidget.Spider;
using SpiderWidget.Targets;

namespace SpiderWidget.Tests;

public class SpiderBrainTests
{
    private const float Dt = Tuning.StepSeconds;

    private static Surface Edge(int left, int top, int width) =>
        new(new Rectangle(left, top, width, 0), SurfaceKind.Floor);

    /// Тот же порядок, что в SpiderForm.Tick: мозг → физика → ноги. Паук начинает стоя на кромке в start.
    private static void Simulate(int seed, Surface[] surfaces, Vector2 start, float seconds, Action<Physics, Legs> eachStep)
    {
        var brain = new SpiderBrain(new Random(seed));
        var body = new Physics();
        body.PlaceOn(start);
        var legs = new Legs(3);
        legs.Place(body.Position);
        for (float t = 0; t < seconds; t += Dt)
        {
            brain.Step(Dt, body, surfaces);
            body.Step(Dt, surfaces);
            legs.Update(Dt, body.Position, brain.Hold, body.Velocity.X);
            eachStep(body, legs);
        }
    }

    private static Surface Icon(int left, int top) =>
        new(new Rectangle(left, top, 60, 0), SurfaceKind.Icon);

    /// Паук, посаженный на кромку опоры.
    private static (SpiderBrain Brain, Physics Body, Legs Legs) SittingOn(int seed, Surface surface)
    {
        var brain = new SpiderBrain(new Random(seed));
        var body = new Physics();
        body.PlaceOn(new Vector2(surface.Bounds.Left + surface.Bounds.Width / 2f, surface.Top));
        var legs = new Legs(3);
        legs.Place(body.Position);
        return (brain, body, legs);
    }

    private static void StepAll(SpiderBrain brain, Physics body, Legs legs, Surface[] surfaces)
    {
        brain.Step(Dt, body, surfaces);
        body.Step(Dt, surfaces);
        legs.Update(Dt, body.Position, brain.Hold, body.Velocity.X);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public void SlipFromIcon_EndsLandingOnSurfaceBelow(int seed)
    {
        Surface icon = Icon(1000, 500);
        Surface[] surfaces = [icon, Edge(0, 1000, 2000)];
        (SpiderBrain brain, Physics body, Legs legs) = SittingOn(seed, icon);
        bool slipped = false;

        for (float t = 0; t < 600; t += Dt)
        {
            StepAll(brain, body, legs, surfaces);
            slipped |= brain.State == SpiderState.Slip;
            if (slipped && body.Grounded && body.FindSupport(surfaces) is { Kind: SurfaceKind.Floor })
            {
                return; // сорвался с иконки и приземлился на пол под ней
            }
        }

        Assert.Fail(slipped ? "сорвался, но не приземлился на пол" : "за 10 минут ни разу не сорвался");
    }

    [Fact]
    public void Hanging_FeetGripIconEdge()
    {
        Surface icon = Icon(1000, 500);
        Surface[] surfaces = [icon, Edge(0, 1000, 2000)];
        (SpiderBrain brain, Physics body, Legs legs) = SittingOn(1, icon);
        int hangingSteps = 0;

        for (float t = 0; t < 5; t += Dt)
        {
            StepAll(brain, body, legs, surfaces);
            if (brain.State != SpiderState.Hang)
            {
                continue;
            }

            hangingSteps++;
            Assert.All(legs.All, l => Assert.InRange(l.Foot.Y, icon.Top - 1, icon.Top + 1));
            Assert.All(legs.All, l => Assert.InRange(l.Foot.X, icon.Bounds.Left - 1, icon.Bounds.Right + 1));
        }

        Assert.True(hangingSteps > 0);
    }

    [Fact]
    public void Hanging_SlipsAboutThreePercentPerSecond()
    {
        Surface icon = Icon(1000, 500);
        Surface[] surfaces = [icon, Edge(0, 1000, 2000)];
        (SpiderBrain brain, Physics body, Legs legs) = SittingOn(42, icon);
        float hangingSeconds = 0;
        int slips = 0;

        while (hangingSeconds < 10_000)
        {
            SpiderState before = brain.State;
            StepAll(brain, body, legs, surfaces);
            if (before == SpiderState.Hang)
            {
                hangingSeconds += Dt;
            }

            if (before == SpiderState.Hang && brain.State == SpiderState.Slip)
            {
                slips++;
            }

            if (body.Grounded && body.FindSupport(surfaces) is { Kind: SurfaceKind.Floor })
            {
                body.PlaceOn(new Vector2(1030, icon.Top)); // обратно на иконку — копим время висения
            }
        }

        Assert.InRange(slips / hangingSeconds, 0.025, 0.035);
    }

    // Иконки обычно столбцом у края экрана, а паук появляется посередине: должен сам до них дойти.
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Climbing_IconFarAcrossTheFloor_ReachedWithinTwoMinutes(int seed)
    {
        Surface icon = Icon(100, 870);
        Surface[] surfaces = [icon, Edge(0, 1000, 2000)];
        var brain = new SpiderBrain(new Random(seed));
        var body = new Physics();
        body.PlaceOn(new Vector2(960, 1000));
        var legs = new Legs(3);
        legs.Place(body.Position);

        for (float t = 0; t < 120; t += Dt)
        {
            StepAll(brain, body, legs, surfaces);
            if (brain.State == SpiderState.Hang)
            {
                return;
            }
        }

        Assert.Fail("за 2 минуты так и не забрался на иконку в 860 px от себя");
    }

    [Fact]
    public void Climbing_FromFloorReachesIconAbove()
    {
        Surface icon = Icon(1000, 800);
        Surface[] surfaces = [icon, Edge(0, 1000, 2000)];
        var brain = new SpiderBrain(new Random(3));
        var body = new Physics { Position = new Vector2(900, 1000) };
        var legs = new Legs(3);
        legs.Place(body.Position);

        for (float t = 0; t < 300; t += Dt)
        {
            StepAll(brain, body, legs, surfaces);
            if (brain.State == SpiderState.Hang)
            {
                Assert.Equal(icon.Top, body.Position.Y);
                return;
            }
        }

        Assert.Fail("за 5 минут так и не забрался на иконку");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Walking_NeverBothTripodsInStepAtOnce(int seed)
    {
        var groupsSeen = new HashSet<int>();
        float startX = 1000;
        float maxShift = 0;

        Simulate(seed, [Edge(0, 1000, 2000)], new Vector2(startX, 1000), 30, (body, legs) =>
        {
            int[] stepping = legs.All.Where(l => l.Stepping).Select(l => l.Group).Distinct().ToArray();
            Assert.True(stepping.Length <= 1, "шагают обе тройки сразу");
            groupsSeen.UnionWith(stepping);
            maxShift = Math.Max(maxShift, Math.Abs(body.Position.X - startX));
        });

        Assert.True(maxShift > 50, "паук должен был пройтись");
        Assert.Equal([0, 1], groupsSeen.Order());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    public void Walking_TurnsAtEdge_StaysOnSurface(int seed)
    {
        Simulate(seed, [Edge(0, 1000, 300)], new Vector2(150, 1000), 60, (body, _) =>
        {
            Assert.True(body.Grounded);
            Assert.InRange(body.Position.X, Tuning.EdgeMargin - 1, 300 - Tuning.EdgeMargin + 1);
        });
    }

    [Fact]
    public void SurfaceNarrowerThanLegs_SpiderStaysPut()
    {
        Simulate(1, [Edge(0, 1000, 60)], new Vector2(30, 1000), 20, (body, _) =>
        {
            Assert.Equal(30, body.Position.X);
        });
    }

    [Fact]
    public void Standing_FeetOnEdgeWhenNotStepping()
    {
        Simulate(1, [Edge(0, 1000, 2000)], new Vector2(1000, 1000), 20, (_, legs) =>
        {
            Assert.All(legs.All.Where(l => !l.Stepping), l => Assert.InRange(l.Foot.Y, 999, 1001));
        });
    }
}
