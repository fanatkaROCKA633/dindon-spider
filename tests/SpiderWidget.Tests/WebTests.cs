using System.Drawing;
using System.Numerics;
using SpiderWidget.Spider;
using SpiderWidget.Targets;

namespace SpiderWidget.Tests;

public class WebTests
{
    private const float Dt = Tuning.StepSeconds;
    private static readonly RectangleF Area = new(0, 0, 1920, 1000);
    private static readonly Surface[] Floor = [new(new Rectangle(0, 1000, 1920, 0), SurfaceKind.Floor)];

    private static Web Woven(Corner corner)
    {
        var web = new Web(corner, corner == Corner.TopLeft ? new Vector2(0, 0) : new Vector2(1920, 0), Tuning.WebRadius);
        web.Weave(Tuning.WeaveSeconds);
        return web;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Strands_StayInsideCornerSquare(bool topLeft)
    {
        Web web = Woven(topLeft ? Corner.TopLeft : Corner.TopRight);
        RectangleF bounds = web.Bounds;
        bounds.Inflate(0.5f, 0.5f);

        Assert.All(web.Strands, s =>
        {
            Assert.True(bounds.Contains(s.A.X, s.A.Y) && bounds.Contains(s.B.X, s.B.Y), $"нить {s} вне угла");
        });
        Assert.True(web.Contains(web.Seat), "место паука — на паутине");
    }

    [Fact]
    public void Weaving_GrowsStrandByStrandUntilFinished()
    {
        var web = new Web(Corner.TopLeft, Vector2.Zero, Tuning.WebRadius);
        Assert.Equal(0, web.BuiltCount);

        int previous = 0;
        for (float t = 0; t < Tuning.WeaveSeconds + 1; t += Dt)
        {
            web.Weave(Dt);
            Assert.True(web.BuiltCount >= previous);
            previous = web.BuiltCount;
        }

        Assert.True(web.Finished);
        Assert.Equal(web.Strands.Count, web.BuiltCount);
    }

    [Fact]
    public void Wipe_TearsOnlyStrandsTheCursorTouched()
    {
        Web web = Woven(Corner.TopLeft);

        Assert.Equal(0, web.Wipe(new Vector2(600, 600), new Vector2(700, 650))); // далеко от паутины
        int torn = web.Wipe(new Vector2(120, 10), new Vector2(120, 60));         // короткий мазок у верхнего края

        Assert.InRange(torn, 1, web.Strands.Count / 2);
        Assert.False(web.Gone);
    }

    [Fact]
    public void Wipe_SeveralSweepsAcross_WebIsGone()
    {
        Web web = Woven(Corner.TopLeft);

        for (int y = 10; y < Tuning.WebRadius; y += 20)
        {
            web.Wipe(new Vector2(0, y), new Vector2(Tuning.WebRadius, y)); // «смахнул рукой» сверху вниз
        }

        Assert.True(web.Gone);
    }

    [Fact]
    public void Corner_BecomesQuietAfterAMinuteWithoutCursor()
    {
        var webs = new Webs(Area);
        var cursor = new Vector2(960, 500); // посередине экрана, ни в каком углу

        for (float t = 0; t < Tuning.WebQuietSeconds - 1; t += 0.5f)
        {
            webs.Update(0.5f, cursor);
        }

        Assert.Null(webs.QuietCorner());

        webs.Update(2, cursor);
        Assert.NotNull(webs.QuietCorner());
    }

    [Fact]
    public void CursorInCorner_ResetsOnlyThatCorner()
    {
        var webs = new Webs(Area);
        for (float t = 0; t < Tuning.WebQuietSeconds + 1; t += 0.5f)
        {
            webs.Update(0.5f, new Vector2(50, 50)); // всё время в левом верхнем углу
        }

        Assert.Equal(Corner.TopRight, webs.QuietCorner());
    }

    [Fact]
    public void CursorSweepingOverWeb_RemovesIt()
    {
        var webs = new Webs(Area);
        Web web = webs.Begin(Corner.TopRight);
        web.Weave(Tuning.WeaveSeconds);

        for (int y = 10; y < Tuning.WebRadius; y += 20)
        {
            webs.Update(Dt, new Vector2(1920 - Tuning.WebRadius, y));
            webs.Update(Dt, new Vector2(1920, y));
        }

        Assert.False(webs.Has(web));
        Assert.Empty(webs.All);
    }

    [Fact]
    public void Disabled_ClearsWebsAndNoQuietCorners()
    {
        var webs = new Webs(Area);
        webs.Begin(Corner.TopLeft).Weave(Tuning.WeaveSeconds);

        webs.Enabled = false;
        webs.Update(Tuning.WebQuietSeconds * 2, null);

        Assert.Empty(webs.All);
        Assert.Null(webs.QuietCorner());
    }

    // Паук на полу; курсор посередине — углы тихие. Через минуту он должен сам сплести паутину и сесть на неё.
    private static (SpiderBrain Brain, Physics Body, Webs Webs) SpiderUnderWebs(int seed)
    {
        var webs = new Webs(Area);
        var brain = new SpiderBrain(new Random(seed)) { Webs = webs, Cursor = new Vector2(960, 500) };
        var body = new Physics { World = new RectangleF(54, 0, 1812, 1000) };
        body.PlaceOn(new Vector2(960, 1000));
        return (brain, body, webs);
    }

    private static void Step(SpiderBrain brain, Physics body, Webs webs)
    {
        brain.Step(Dt, body, Floor);
        body.Step(Dt, Floor);
        webs.Update(Dt, brain.Cursor);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void QuietCorner_SpiderWeavesWebAndSitsOnIt(int seed)
    {
        (SpiderBrain brain, Physics body, Webs webs) = SpiderUnderWebs(seed);

        for (float t = 0; t < 180; t += Dt)
        {
            Step(brain, body, webs);
            if (brain.State == SpiderState.OnWeb)
            {
                Web web = Assert.Single(webs.All);
                Assert.True(web.Finished, "сел на недоплетённую паутину");
                Assert.True(t >= Tuning.WebQuietSeconds, $"сплёл раньше минуты: {t:F0} с");
                Assert.True(Vector2.Distance(body.Position, web.Seat) < Tuning.WebSitDrop + 1);
                return;
            }
        }

        Assert.Fail($"за 3 минуты не сел на паутину; состояние {brain.State}, паутин {webs.All.Count}");
    }

    [Fact]
    public void OnWeb_DescendsOnThreadAndClimbsBack()
    {
        (SpiderBrain brain, Physics body, Webs webs) = SpiderUnderWebs(4);
        bool sat = false;
        bool hung = false;

        for (float t = 0; t < 600; t += Dt)
        {
            Step(brain, body, webs);
            sat |= brain.State == SpiderState.OnWeb;
            if (sat && brain.State == SpiderState.Dangle)
            {
                hung = true;
                Assert.Equal(webs.All.Single().Seat, brain.ThreadAnchor);
                Assert.True(body.Position.Y > webs.All.Single().Seat.Y + Tuning.WebSitDrop, "висит ниже паутины");
            }

            if (hung && brain.State == SpiderState.OnWeb)
            {
                return; // спустился, повисел и вернулся
            }
        }

        Assert.Fail(hung ? "повис, но не поднялся обратно" : "за 10 минут ни разу не повис на нити");
    }

    [Fact]
    public void WebWipedWhileHanging_SpiderFallsAndLands()
    {
        (SpiderBrain brain, Physics body, Webs webs) = SpiderUnderWebs(4);
        float t = 0;
        for (; t < 600 && brain.State != SpiderState.Dangle; t += Dt)
        {
            Step(brain, body, webs);
        }

        Assert.Equal(SpiderState.Dangle, brain.State);
        webs.Enabled = false; // как будто паутину убрали

        for (float s = 0; s < 5; s += Dt)
        {
            Step(brain, body, webs);
        }

        Assert.True(body.Grounded);
        Assert.Equal(1000, body.Position.Y);
        Assert.Null(brain.ThreadAnchor);
    }

    [Fact]
    public void CursorAcrossThread_CutsItAndSpiderFalls()
    {
        (SpiderBrain brain, Physics body, Webs webs) = SpiderUnderWebs(4);
        for (float t = 0; t < 600 && brain.State != SpiderState.Dangle; t += Dt)
        {
            Step(brain, body, webs);
        }

        Assert.Equal(SpiderState.Dangle, brain.State);
        Vector2 seat = brain.ThreadAnchor!.Value;
        float y = seat.Y + 5; // чуть ниже места на паутине, поперёк нити
        brain.Cursor = new Vector2(seat.X - 40, y);
        Step(brain, body, webs);
        brain.Cursor = new Vector2(seat.X + 40, y);
        Step(brain, body, webs);

        Assert.Equal(SpiderState.Fall, brain.State);
        Assert.True(webs.All.Count == 1, "нить порвана, а паутина осталась");
    }
}
