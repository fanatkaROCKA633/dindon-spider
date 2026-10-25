using System.Drawing;
using System.Numerics;
using SpiderWidget.Spider;
using SpiderWidget.Targets;

namespace SpiderWidget.Tests;

public class HuntTests
{
    private const float Dt = Tuning.StepSeconds;

    private static Surface Floor(int left, int top, int width) =>
        new(new Rectangle(left, top, width, 0), SurfaceKind.Floor);

    [Theory]
    [InlineData(500, 1000, 650, 820)]  // вверх-вправо
    [InlineData(500, 1000, 300, 900)]  // невысоко влево
    [InlineData(500, 600, 700, 900)]   // вниз
    [InlineData(500, 1000, 520, 700)]  // почти вертикально вверх
    public void JumpVelocity_NoSurfacesOnTheWay_HitsTarget(float fromX, float fromY, float toX, float toY)
    {
        var from = new Vector2(fromX, fromY);
        var to = new Vector2(toX, toY);
        (Vector2 velocity, int steps) = SpiderBrain.JumpVelocity(from, to, Tuning.Gravity, Dt);
        var body = new Physics { Position = from, Velocity = velocity };

        for (int i = 0; i < steps; i++)
        {
            body.Step(Dt, []);
        }

        Assert.True(Vector2.Distance(body.Position, to) <= 2, $"оказался в {body.Position}, цель {to}");
    }

    private static List<float> HuntStartTimes(int seed, Vector2 cursor, float seconds)
    {
        Surface[] surfaces = [Floor(0, 1000, 2000)];
        var brain = new SpiderBrain(new Random(seed)) { Cursor = cursor };
        var body = new Physics();
        body.PlaceOn(new Vector2(1000, 1000));
        var starts = new List<float>();
        SpiderState before = brain.State;
        for (float t = 0; t < seconds; t += Dt)
        {
            brain.Step(Dt, body, surfaces);
            body.Step(Dt, surfaces);
            if (brain.State == SpiderState.Hunt && before != SpiderState.Hunt)
            {
                starts.Add(t);
            }

            before = brain.State;
        }

        return starts;
    }

    [Fact]
    public void CursorFarAway_NeverHunts()
    {
        Assert.Empty(HuntStartTimes(1, new Vector2(1000, 500), 600)); // курсор в 460 px от головы
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void CursorNear_HuntsButNotMoreOftenThanCooldown(int seed)
    {
        List<float> starts = HuntStartTimes(seed, new Vector2(1100, 850), 600);

        Assert.True(starts.Count >= 5, $"за 10 минут рядом с курсором охотился всего {starts.Count} раз");
        for (int i = 1; i < starts.Count; i++)
        {
            Assert.True(starts[i] - starts[i - 1] >= Tuning.HuntCooldownSeconds, $"охоты в {starts[i - 1]:F1} и {starts[i]:F1} с");
        }
    }

    [Fact]
    public void Hunt_EndsWithJumpTowardCursorAndLanding()
    {
        Surface[] surfaces = [Floor(0, 1000, 2000)];
        var cursor = new Vector2(1150, 850);
        var brain = new SpiderBrain(new Random(3)) { Cursor = cursor };
        var body = new Physics();
        body.PlaceOn(new Vector2(1000, 1000));
        bool jumped = false;
        float closest = float.MaxValue;

        for (float t = 0; t < 300; t += Dt)
        {
            brain.Step(Dt, body, surfaces);
            body.Step(Dt, surfaces);
            if (brain.State == SpiderState.Pounce)
            {
                jumped = true;
                closest = Math.Min(closest, Vector2.Distance(body.Position, cursor + new Vector2(0, Tuning.PounceOffsetY)));
            }

            if (jumped && brain.State == SpiderState.Land)
            {
                Assert.True(closest <= 40, $"ближе всего был в {closest:F0} px от цели");
                return;
            }
        }

        Assert.Fail(jumped ? "прыгнул, но не приземлился" : "за 5 минут ни разу не прыгнул на курсор");
    }
}
