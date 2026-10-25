using System.Drawing;
using System.Numerics;
using SpiderWidget.Spider;
using SpiderWidget.Targets;

namespace SpiderWidget.Tests;

public class SurfacesTests
{
    private static WindowInfo Win(int handle, int left, int top, int width, int height) =>
        new(handle, new Rectangle(left, top, width, height), SurfaceKind.Window);

    private static (int Left, int Right)[] Segments(IEnumerable<Surface> edges, int owner) =>
        edges.Where(e => e.Owner == owner).Select(e => (e.Bounds.Left, e.Bounds.Right)).ToArray();

    [Fact]
    public void SingleWindow_WholeTopEdge()
    {
        List<Surface> edges = Surface.TopEdges([Win(1, 100, 200, 400, 300)]);

        Surface edge = Assert.Single(edges);
        Assert.Equal(new Rectangle(100, 200, 400, 0), edge.Bounds);
        Assert.Equal(new Point(100, 200), edge.OwnerOrigin);
    }

    [Fact]
    public void UpperWindowOverRightPart_LeftPartStays()
    {
        WindowInfo upper = Win(1, 300, 100, 400, 400);
        WindowInfo lower = Win(2, 100, 200, 400, 300);

        List<Surface> edges = Surface.TopEdges([upper, lower]);

        Assert.Equal([(100, 300)], Segments(edges, 2));
        Assert.Equal([(300, 700)], Segments(edges, 1));
    }

    [Fact]
    public void UpperWindowOverMiddle_SplitsEdgeInTwo()
    {
        List<Surface> edges = Surface.TopEdges([Win(1, 200, 150, 100, 100), Win(2, 100, 200, 400, 300)]);

        Assert.Equal([(100, 200), (300, 500)], Segments(edges, 2));
    }

    [Fact]
    public void UpperWindowNotCrossingEdgeLine_DoesNotCover()
    {
        // Верхнее по Z окно целиком ниже кромки — линию y=200 не закрывает.
        List<Surface> edges = Surface.TopEdges([Win(1, 100, 250, 400, 100), Win(2, 100, 200, 400, 300)]);

        Assert.Equal([(100, 500)], Segments(edges, 2));
    }

    [Fact]
    public void FullyCovered_NoEdge()
    {
        List<Surface> edges = Surface.TopEdges([Win(1, 0, 0, 1000, 1000), Win(2, 100, 200, 400, 300)]);

        Assert.Empty(Segments(edges, 2));
    }

    [Fact]
    public void Carry_BodyOnMovedWindow_MovesWithIt()
    {
        List<Surface> before = Surface.TopEdges([Win(1, 100, 500, 400, 300)]);
        var body = new Physics { Position = new Vector2(300, 0) };
        for (int i = 0; i < 120; i++)
        {
            body.Step(Tuning.StepSeconds, before);
        }

        Assert.Equal(new Vector2(300, 500), body.Position);

        List<Surface> after = Surface.TopEdges([Win(1, 200, 450, 400, 300)]);
        body.Carry(before, after);
        body.Step(Tuning.StepSeconds, after);

        Assert.Equal(new Vector2(400, 450), body.Position);
        Assert.True(body.Grounded);
    }

    [Fact]
    public void Carry_WindowGone_BodyFalls()
    {
        List<Surface> before = Surface.TopEdges([Win(1, 100, 500, 400, 300)]);
        var body = new Physics { Position = new Vector2(300, 0) };
        for (int i = 0; i < 120; i++)
        {
            body.Step(Tuning.StepSeconds, before);
        }

        List<Surface> after = [];
        body.Carry(before, after);
        body.Step(Tuning.StepSeconds, after);

        Assert.False(body.Grounded);
    }
}
