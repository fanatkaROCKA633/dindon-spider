using System.Numerics;

namespace SpiderWidget.Spider;

internal enum Corner
{
    TopLeft,
    TopRight,
}

internal readonly record struct Strand(Vector2 A, Vector2 B);

/// Паутина в верхнем углу экрана — «веер»: радиальные нити из угла и провисающие дуги между
/// ними. Паук плетёт её по нитке (сначала радиальные, потом дуги изнутри наружу); курсор,
/// проходя по нитям, рвёт те, которых коснулся.
internal sealed class Web
{
    private const int Radials = 6;
    private static readonly float[] Rings = [0.3f, 0.48f, 0.66f, 0.84f, 1f];
    private const float Sag = 0.86f; // середина дуги ближе к углу — нить провисает

    private readonly bool[] _alive;

    public Web(Corner corner, Vector2 origin, float radius)
    {
        Corner = corner;
        Origin = origin;
        Radius = radius;
        Away = corner == Corner.TopLeft ? 1 : -1;
        Strands = Build(origin, radius, Away);
        _alive = Enumerable.Repeat(true, Strands.Count).ToArray();
        Seat = origin + new Vector2(Away, 1) * (radius * Tuning.WebSeatFraction / MathF.Sqrt(2));
    }

    public Corner Corner { get; }

    public Vector2 Origin { get; }

    public float Radius { get; }

    /// Куда от угла по горизонтали: +1 для левого угла, −1 для правого.
    public int Away { get; }

    /// Точка на паутине, где сидит паук и откуда спускается его нить.
    public Vector2 Seat { get; }

    public IReadOnlyList<Strand> Strands { get; }

    /// Сколько сплетено: 0 — ничего, 1 — готово.
    public float Woven { get; private set; }

    public bool Finished => Woven >= 1;

    /// Растёт при каждом изменении — окну паутины пора перерисоваться.
    public int Version { get; private set; }

    public int BuiltCount => (int)MathF.Ceiling(Woven * Strands.Count);

    public bool IsShown(int index) => index < BuiltCount && _alive[index];

    /// Порвана настолько, что от неё ничего не осталось.
    public bool Gone
    {
        get
        {
            int built = BuiltCount;
            int alive = Enumerable.Range(0, built).Count(i => _alive[i]);
            return built >= 3 && alive < built * Tuning.WebGoneBelow;
        }
    }

    public RectangleF Bounds => new(Away > 0 ? Origin.X : Origin.X - Radius, Origin.Y, Radius, Radius);

    public bool Contains(Vector2 point) => Bounds.Contains(point.X, point.Y);

    public void Weave(float dt)
    {
        int before = BuiltCount;
        Woven = Math.Min(1f, Woven + dt / Tuning.WeaveSeconds);
        if (BuiltCount != before)
        {
            Version++;
        }
    }

    /// Курсор прошёл от from до to: рвёт сплетённые нити, которых коснулся. Возвращает, сколько порвал.
    public int Wipe(Vector2 from, Vector2 to)
    {
        int torn = 0;
        for (int i = 0; i < BuiltCount; i++)
        {
            if (_alive[i] && SegmentDistance(Strands[i].A, Strands[i].B, from, to) <= Tuning.WipeRadius)
            {
                _alive[i] = false;
                torn++;
            }
        }

        if (torn > 0)
        {
            Version++;
        }

        return torn;
    }

    /// Кратчайшее расстояние между отрезками ab и cd (0, если пересекаются).
    public static float SegmentDistance(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        if (Intersect(a, b, c, d))
        {
            return 0;
        }

        return Math.Min(
            Math.Min(PointToSegment(a, c, d), PointToSegment(b, c, d)),
            Math.Min(PointToSegment(c, a, b), PointToSegment(d, a, b)));
    }

    private static List<Strand> Build(Vector2 origin, float radius, int away)
    {
        var directions = Enumerable.Range(0, Radials)
            .Select(i => MathF.PI / 2 * i / (Radials - 1)) // 0 — вдоль верхнего края, π/2 — вдоль бокового
            .Select(a => new Vector2(away * MathF.Cos(a), MathF.Sin(a)))
            .ToArray();

        var strands = directions.Select(d => new Strand(origin, origin + d * radius)).ToList();
        foreach (float ring in Rings)
        {
            for (int i = 0; i < directions.Length - 1; i++)
            {
                Vector2 p = origin + directions[i] * radius * ring;
                Vector2 q = origin + directions[i + 1] * radius * ring;
                Vector2 middle = origin + Vector2.Normalize(directions[i] + directions[i + 1]) * radius * ring * Sag;
                strands.Add(new Strand(p, middle));
                strands.Add(new Strand(middle, q));
            }
        }

        return strands;
    }

    private static float PointToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float lengthSquared = ab.LengthSquared();
        float t = lengthSquared == 0 ? 0 : Math.Clamp(Vector2.Dot(p - a, ab) / lengthSquared, 0, 1);
        return Vector2.Distance(p, a + ab * t);
    }

    private static bool Intersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        static float Cross(Vector2 o, Vector2 p, Vector2 q) => (p.X - o.X) * (q.Y - o.Y) - (p.Y - o.Y) * (q.X - o.X);
        float d1 = Cross(c, d, a);
        float d2 = Cross(c, d, b);
        float d3 = Cross(a, b, c);
        float d4 = Cross(a, b, d);
        return ((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) && ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0));
    }
}

/// Паутины в двух верхних углах рабочей области. Следит, давно ли курсор заходил в каждый угол:
/// где не был минуту — там паук может сплести паутину. Позицию курсора только читает опросом.
internal sealed class Webs(RectangleF area)
{
    private readonly Dictionary<Corner, Web> _webs = [];
    private readonly Dictionary<Corner, float> _quietSeconds = new() { [Corner.TopLeft] = 0, [Corner.TopRight] = 0 };
    private Vector2? _lastCursor;
    private bool _enabled = true;

    /// Рабочая область главного монитора: от её верхних углов растут паутины.
    public RectangleF Area { get; set; } = area;

    /// Выключили — все паутины исчезают и новые не появляются.
    public bool Enabled
    {
        get => _enabled;
        set
        {
            _enabled = value;
            if (!value)
            {
                _webs.Clear();
            }

            foreach (Corner corner in _quietSeconds.Keys.ToList())
            {
                _quietSeconds[corner] = 0;
            }
        }
    }

    public IReadOnlyCollection<Web> All => _webs.Values;

    public Web? At(Corner corner) => _webs.GetValueOrDefault(corner);

    public bool Has(Web web) => _webs.TryGetValue(web.Corner, out Web? current) && current == web;

    public Vector2 Origin(Corner corner) => corner == Corner.TopLeft ? new(Area.Left, Area.Top) : new(Area.Right, Area.Top);

    /// Угол, который ждёт паутину: курсор не заходил туда WebQuietSeconds и паутины там ещё нет.
    public Corner? QuietCorner()
    {
        if (!Enabled)
        {
            return null;
        }

        foreach ((Corner corner, float quiet) in _quietSeconds)
        {
            if (quiet >= Tuning.WebQuietSeconds && !_webs.ContainsKey(corner))
            {
                return corner;
            }
        }

        return null;
    }

    public Web Begin(Corner corner)
    {
        var web = new Web(corner, Origin(corner), Tuning.WebRadius);
        _webs[corner] = web;
        return web;
    }

    /// Раз в кадр: считает тишину в углах, рвёт нити под курсором и убирает порванные паутины.
    public void Update(float dt, Vector2? cursor)
    {
        if (!Enabled)
        {
            return;
        }

        foreach (Corner corner in _quietSeconds.Keys.ToList())
        {
            bool visited = cursor is Vector2 c && Zone(corner).Contains(c.X, c.Y);
            _quietSeconds[corner] = visited ? 0 : _quietSeconds[corner] + dt;
        }

        if (cursor is Vector2 now && _lastCursor is Vector2 last && now != last)
        {
            foreach (Web web in _webs.Values)
            {
                web.Wipe(last, now);
            }
        }

        foreach (Web gone in _webs.Values.Where(w => w.Gone).ToList())
        {
            _webs.Remove(gone.Corner);
        }

        _lastCursor = cursor;
    }

    private RectangleF Zone(Corner corner)
    {
        Vector2 origin = Origin(corner);
        float r = Tuning.WebRadius;
        return new RectangleF(corner == Corner.TopLeft ? origin.X : origin.X - r, origin.Y, r, r);
    }
}
