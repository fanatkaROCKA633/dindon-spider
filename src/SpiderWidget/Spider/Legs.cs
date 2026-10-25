using System.Numerics;

namespace SpiderWidget.Spider;

internal readonly record struct LegPose(Vector2 Hip, Vector2 Knee, Vector2 Foot);

internal enum Footing
{
    Ground, // ходит по кромке, шагает тройками
    Air,    // падает, летит или соскальзывает: ноги поджаты
    Flail,  // держат мышью: ноги болтаются и дрыгаются
    Grip,   // держится за узкую опору (иконку): ступни прижаты к её кромке от Left до Right
    Wall,   // лезет по боковой грани WallX; WallSide — с какой стороны от тела стена
    Spread, // сидит на паутине: лапы раскинуты звездой вокруг головы
}

/// Поза паука — это решает SpiderBrain. Crouch — присел перед прыжком. MouthOpen и HeadShift
/// (в логических пикселях) — только для отрисовки головы: открыл рот, мотает головой.
internal readonly record struct Hold(
    Footing Footing,
    float Left = 0,
    float Right = 0,
    float WallX = 0,
    int WallSide = 0,
    bool Crouch = false,
    bool MouthOpen = false,
    int HeadShift = 0);

/// Одна нога. HipOffset — от центра головы, RestOffset — от точки опоры тела.
/// Side: −1 левая, +1 правая. Group: тройка походки, 0 или 1. Row: 0 — передняя, 2 — задняя.
internal sealed class Leg(Vector2 hipOffset, Vector2 restOffset, int side, int group, int row)
{
    public Vector2 HipOffset { get; } = hipOffset;

    public Vector2 RestOffset { get; } = restOffset;

    public int Side { get; } = side;

    public int Group { get; } = group;

    public int Row { get; } = row;

    public Vector2 Foot { get; set; }

    public bool Stepping { get; private set; }

    private Vector2 _stepFrom;
    private Vector2 _stepTo;
    private float _stepProgress;

    public void StartStep(Vector2 to)
    {
        Stepping = true;
        _stepFrom = Foot;
        _stepTo = to;
        _stepProgress = 0;
    }

    public void CancelStep() => Stepping = false;

    /// Ступня летит к цели по дуге и встаёт на неё, когда шаг закончен.
    public void AdvanceStep(float progressDelta, float lift)
    {
        _stepProgress = Math.Min(_stepProgress + progressDelta, 1f);
        Vector2 straight = Vector2.Lerp(_stepFrom, _stepTo, _stepProgress);
        Foot = straight - new Vector2(0, MathF.Sin(MathF.PI * _stepProgress) * lift);
        if (_stepProgress >= 1f)
        {
            Foot = _stepTo;
            Stepping = false;
        }
    }
}

/// Шесть ног, по три с каждой стороны, походка тройками. Координаты — те же
/// физические пиксели, что у Physics.
internal sealed class Legs
{
    // Размеры в логических пикселях спрайта, до умножения на масштаб.
    public const float UpperLength = 9;
    public const float LowerLength = 9;
    public const int DefaultHeadHeight = 38; // самая высокая голова, под которую рассчитано окно паука
    private const float HeadLift = 4; // низ головы над точкой опоры
    private const float StepThreshold = 4; // ступня шагает, когда отстала дальше
    private const float StepLift = 3;
    private const float StepSeconds = 0.12f;
    private const float LeadSeconds = 0.15f; // при ходьбе ступня ставится с упреждением по ходу
    private const float WallRowSpacing = 8; // по стене ступни разнесены по высоте
    private const float WallScrabble = 2;   // и чуть дёргаются — видно, что перебирает
    private const float WallScrabbleSpeed = 14;
    private const float TuckSpread = 0.6f;
    private const float TuckLift = 4;
    private const float FlailKick = 3;
    private const float FlailSpeed = 18;
    private const float CrouchDrop = 4;
    private const float SpreadReach = 0.9f;
    private static readonly float[] SpreadAngles = [-0.7f, 0.15f, 0.9f]; // по рядам, радианы; y вниз


    // Плечи — под низом головы, HipInset пикселей выше её нижнего края; так ноги одинаково
    // торчат из-под любой головы, какой бы высоты она ни была.
    private static readonly (float HipX, float HipInset, float RestX)[] Layout =
    [
        (5, 5, 8),
        (7, 6, 14),
        (8, 8, 18),
    ];

    private readonly float _scale;
    private int _lastGroup = 1;
    private float _phase;
    private Footing _lastFooting = Footing.Ground;
    private bool _crouch;

    /// headHeight — высота спрайта головы в логических пикселях: от неё зависит, где плечи.
    public Legs(float scale, int headHeight = DefaultHeadHeight)
    {
        _scale = scale;
        float half = headHeight / 2f;
        BodyHeight = half + HeadLift;
        var legs = new List<Leg>();
        for (int row = 0; row < Layout.Length; row++)
        {
            (float hipX, float hipInset, float restX) = Layout[row];
            float hipY = half - hipInset;
            foreach (int side in new[] { -1, 1 })
            {
                // Тройки как у насекомых: передняя и задняя одной стороны + средняя другой.
                int group = (row + (side > 0 ? 1 : 0)) % 2;
                legs.Add(new Leg(new Vector2(side * hipX, hipY) * scale, new Vector2(side * restX, 0) * scale, side, group, row));
            }
        }

        All = legs;
    }

    public IReadOnlyList<Leg> All { get; }

    /// От точки опоры до центра головы, в логических пикселях.
    public float BodyHeight { get; }

    public bool Animating => All.Any(l => l.Stepping);

    /// Центр головы. Пока тройка в шаге, голова приподнята на пиксель — так видно покачивание;
    /// перед прыжком опущена — присел.
    public Vector2 BodyCenter(Vector2 ground) =>
        ground - new Vector2(0, (BodyHeight + (Animating ? 1 : 0) - (_crouch ? CrouchDrop : 0)) * _scale);

    /// Ставит все ступни в позицию покоя без шагов.
    public void Place(Vector2 ground)
    {
        foreach (Leg leg in All)
        {
            leg.CancelStep();
            leg.Foot = ground + leg.RestOffset;
        }
    }

    public void Update(float dt, Vector2 ground, Hold hold, float velocityX)
    {
        // Только что приземлился (слез со стены, отпустили иконку) — ступни сразу встают на кромку,
        // а не ждут своей очереди шагнуть, вися в воздухе.
        if (hold.Footing == Footing.Ground && _lastFooting != Footing.Ground)
        {
            Place(ground);
        }

        _lastFooting = hold.Footing;
        _crouch = hold.Crouch;
        switch (hold.Footing)
        {
            case Footing.Air:
                Tuck(ground);
                return;
            case Footing.Flail:
                _phase += dt * FlailSpeed;
                Flail(ground);
                return;
            case Footing.Spread:
                Spread(ground);
                return;
            case Footing.Grip:
                Grip(ground, hold.Left, hold.Right);
                return;
            case Footing.Wall:
                _phase += dt * WallScrabbleSpeed;
                Climb(ground, hold.WallX, hold.WallSide);
                return;
        }

        foreach (Leg leg in All.Where(l => l.Stepping))
        {
            leg.AdvanceStep(dt / StepSeconds, StepLift * _scale);
        }

        if (Animating)
        {
            return;
        }

        // Никто не шагает — даём шагнуть тройке, которая ждала дольше, если ей нужно.
        foreach (int group in new[] { 1 - _lastGroup, _lastGroup })
        {
            Leg[] lagging = All.Where(l => l.Group == group && Distance(l, ground, velocityX) > StepThreshold * _scale).ToArray();
            if (lagging.Length == 0)
            {
                continue;
            }

            foreach (Leg leg in All.Where(l => l.Group == group))
            {
                leg.StartStep(Target(leg, ground, velocityX));
            }

            _lastGroup = group;
            return;
        }
    }

    public IEnumerable<LegPose> Poses(Vector2 ground)
    {
        Vector2 center = BodyCenter(ground);
        foreach (Leg leg in All)
        {
            Vector2 hip = center + leg.HipOffset;
            (Vector2 knee, Vector2 foot) = SolveIk(hip, leg.Foot, UpperLength * _scale, LowerLength * _scale, leg.Side);
            yield return new LegPose(hip, knee, foot);
        }
    }

    // В полёте ступни подтянуты к телу и чуть вверх.
    // На паутине: передние лапы вверх-в стороны, средние в стороны, задние вниз — как паук на сети.
    private void Spread(Vector2 ground)
    {
        Vector2 center = BodyCenter(ground);
        float reach = (UpperLength + LowerLength) * SpreadReach * _scale;
        foreach (Leg leg in All)
        {
            leg.CancelStep();
            float angle = SpreadAngles[leg.Row];
            leg.Foot = center + new Vector2(leg.Side * MathF.Cos(angle), MathF.Sin(angle)) * reach;
        }
    }

    private void Tuck(Vector2 ground)
    {
        foreach (Leg leg in All)
        {
            leg.CancelStep();
            leg.Foot = ground + new Vector2(leg.RestOffset.X * TuckSpread, -TuckLift * _scale);
        }
    }

    // В руке ноги свисают и дрыгаются вразнобой.
    private void Flail(Vector2 ground)
    {
        foreach (Leg leg in All)
        {
            leg.CancelStep();
            float kick = MathF.Sin(_phase + leg.Row * 2.1f + leg.Side) * FlailKick * _scale;
            leg.Foot = ground + new Vector2(leg.RestOffset.X * 0.8f + kick * 0.5f, kick);
        }
    }

    // Опора уже тела: ступни, которым не хватило кромки, сползают к её краям — паук обхватывает иконку.
    private void Grip(Vector2 ground, float left, float right)
    {
        foreach (Leg leg in All)
        {
            leg.CancelStep();
            leg.Foot = new Vector2(Math.Clamp(ground.X + leg.RestOffset.X, left, right), ground.Y);
        }
    }

    // Ноги со стороны стены цепляются за неё одна над другой, остальные поджаты.
    private void Climb(Vector2 ground, float wallX, int wallSide)
    {
        Vector2 center = BodyCenter(ground);
        foreach (Leg leg in All)
        {
            leg.CancelStep();
            if (leg.Side == wallSide)
            {
                float y = (leg.Row - 1) * WallRowSpacing + MathF.Sin(_phase + leg.Row * 2) * WallScrabble;
                leg.Foot = new Vector2(wallX, center.Y + y * _scale);
            }
            else
            {
                leg.Foot = ground + leg.RestOffset * 0.5f;
            }
        }
    }

    private static Vector2 Target(Leg leg, Vector2 ground, float velocityX) =>
        ground + leg.RestOffset + new Vector2(velocityX * LeadSeconds, 0);

    private static float Distance(Leg leg, Vector2 ground, float velocityX) =>
        Vector2.Distance(leg.Foot, Target(leg, ground, velocityX));

    /// Аналитическая IK для двух звеньев. Если цель дальше вытянутой ноги — нога тянется к ней по прямой.
    public static (Vector2 Knee, Vector2 End) SolveIk(Vector2 hip, Vector2 target, float upper, float lower, int side)
    {
        Vector2 toTarget = target - hip;
        float distance = toTarget.Length();
        Vector2 direction = distance > 1e-3f ? toTarget / distance : new Vector2(side, 0);

        if (distance >= upper + lower)
        {
            return (hip + direction * upper, hip + direction * (upper + lower));
        }

        float d = Math.Max(distance, Math.Abs(upper - lower) + 1e-3f);
        float cos = Math.Clamp((upper * upper + d * d - lower * lower) / (2 * upper * d), -1f, 1f);

        // Колено наружу и вверх. В экранных координатах (y вниз) поворот на −angle
        // поднимает колено правой ноги, на +angle — левой.
        float angle = -side * MathF.Acos(cos);
        var kneeDirection = new Vector2(
            direction.X * MathF.Cos(angle) - direction.Y * MathF.Sin(angle),
            direction.X * MathF.Sin(angle) + direction.Y * MathF.Cos(angle));
        Vector2 knee = hip + kneeDirection * upper;
        Vector2 end = knee + Vector2.Normalize(target - knee) * lower;
        return (knee, end);
    }
}
