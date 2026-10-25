namespace SpiderWidget.Targets;

internal enum SurfaceKind
{
    Floor,
    Window,
    Taskbar,
    Icon,
}

/// Окно-кандидат в опоры. Handle — чтобы узнавать то же окно после обновления списка.
internal readonly record struct WindowInfo(nint Handle, Rectangle Bounds, SurfaceKind Kind);

/// Опора: паук стоит на её верхней кромке, от Left до Right. Координаты — физические пиксели экрана.
/// Owner и OwnerOrigin — окно, которому принадлежит кромка, и его левый верхний угол (0 для пола).
internal readonly record struct Surface(Rectangle Bounds, SurfaceKind Kind, nint Owner = 0, Point OwnerOrigin = default)
{
    private const int MinEdgeWidth = 10;

    public int Top => Bounds.Top;

    public bool SpansX(float x) => x >= Bounds.Left && x <= Bounds.Right;

    /// Низ рабочей области каждого монитора — пол, ниже которого паук не падает.
    public static IReadOnlyList<Surface> Floors(IEnumerable<Rectangle> workAreas) =>
        workAreas.Select(a => new Surface(new Rectangle(a.Left, a.Bottom, a.Width, 0), SurfaceKind.Floor)).ToList();

    /// Видимые куски верхних кромок окон. Окна — в Z-порядке сверху вниз: кромку окна
    /// закрывает любое окно выше него, через которое проходит линия этой кромки.
    public static List<Surface> TopEdges(IReadOnlyList<WindowInfo> topToBottom)
    {
        var edges = new List<Surface>();
        for (int i = 0; i < topToBottom.Count; i++)
        {
            WindowInfo window = topToBottom[i];
            int y = window.Bounds.Top;
            var visible = new List<(int Left, int Right)> { (window.Bounds.Left, window.Bounds.Right) };
            for (int j = 0; j < i; j++)
            {
                Rectangle above = topToBottom[j].Bounds;
                if (above.Top <= y && y < above.Bottom)
                {
                    visible = Subtract(visible, above.Left, above.Right);
                }
            }

            foreach ((int left, int right) in visible.Where(s => s.Right - s.Left >= MinEdgeWidth))
            {
                edges.Add(new Surface(new Rectangle(left, y, right - left, 0), window.Kind, window.Handle, window.Bounds.Location));
            }
        }

        return edges;
    }

    private static List<(int Left, int Right)> Subtract(List<(int Left, int Right)> segments, int left, int right)
    {
        var result = new List<(int Left, int Right)>();
        foreach ((int segLeft, int segRight) in segments)
        {
            if (right <= segLeft || left >= segRight)
            {
                result.Add((segLeft, segRight));
                continue;
            }

            if (segLeft < left)
            {
                result.Add((segLeft, left));
            }

            if (right < segRight)
            {
                result.Add((right, segRight));
            }
        }

        return result;
    }
}
