using System.Numerics;
using SpiderWidget.Targets;

namespace SpiderWidget.Spider;

/// Паутина: поход к краю экрана и подъём в верхний угол, плетение, посиделки на паутине,
/// спуск на нити, висение и подъём обратно. Пока паук на паутине, телом управляет мозг
/// (Physics.Kinematic). Паутину смахнули, выключили или курсор перерезал нить — паук падает.
internal sealed partial class SpiderBrain
{
    private Web? _web;
    private float _webStayLeft;
    private float _threadTargetY;
    private float _webPhase;
    private Vector2? _lastCursorOnThread;

    /// Паутины этого экрана; null — паук про паутину ничего не знает (так в части тестов).
    public Webs? Webs { get; set; }

    /// Откуда идёт нить, пока паук на ней спускается, висит или поднимается; иначе null.
    public Vector2? ThreadAnchor =>
        _web != null && State is SpiderState.Descend or SpiderState.Dangle or SpiderState.Ascend or SpiderState.WebLeave
            ? _web.Seat
            : null;

    // После паузы на опоре: в тихий угол — плести; или, иногда, обратно на готовую паутину.
    private bool TryStartWebTrip(Physics body, Surface support)
    {
        if (Webs is not { Enabled: true } webs)
        {
            return false;
        }

        Web? target = null;
        if (webs.QuietCorner() is Corner quiet)
        {
            target = new Web(quiet, webs.Origin(quiet), Tuning.WebRadius); // пока план: сплетёт, когда доберётся
        }
        else
        {
            List<Web> finished = webs.All.Where(w => w.Finished).ToList();
            if (finished.Count > 0 && Roll() < Tuning.ReturnToWebChance)
            {
                target = finished[Pick(finished.Count)];
            }
        }

        if (target is null)
        {
            return false;
        }

        // Лезть по краю экрана можно, только если до него можно дойти по текущей опоре и паук ниже паутины.
        float climbX = EdgeClimbX(target);
        bool reachable = climbX >= support.Bounds.Left - Tuning.WallSideTolerance
            && climbX <= support.Bounds.Right + Tuning.WallSideTolerance
            && body.Position.Y > SitPoint(target).Y + Tuning.ClimbMinHeight;
        if (!reachable)
        {
            return false;
        }

        _web = target;
        _approachX = Math.Clamp(climbX, support.Bounds.Left, support.Bounds.Right);
        State = SpiderState.WebApproach;
        return true;
    }

    // Возвращает true, если шаг уже обработан здесь.
    private bool StepWeb(float dt, Physics body, IReadOnlyList<Surface> surfaces)
    {
        switch (State)
        {
            case SpiderState.WebApproach:
                return body.Grounded && WebApproach(dt, body);
            case SpiderState.EdgeClimb:
                EdgeClimb(dt, body);
                return true;
            case SpiderState.WebWalk:
                WebWalk(dt, body);
                return true;
            case SpiderState.Weave:
                Weave(dt, body);
                return true;
            case SpiderState.OnWeb:
                OnWeb(dt, body, surfaces);
                return true;
            case SpiderState.Descend or SpiderState.Dangle or SpiderState.Ascend or SpiderState.WebLeave:
                OnThread(dt, body, surfaces);
                return true;
            default:
                _lastCursorOnThread = null;
                return false;
        }
    }

    private bool WebApproach(float dt, Physics body)
    {
        Hold = new Hold(Footing.Ground);
        if (Webs is not { Enabled: true } || _web is null)
        {
            State = SpiderState.Idle;
            return false;
        }

        float dx = _approachX - body.Position.X;
        if (Math.Abs(dx) > Tuning.WebTripSpeed * dt)
        {
            Facing = Math.Sign(dx);
            body.Velocity = new Vector2(Facing * Tuning.WebTripSpeed, 0);
            return true;
        }

        body.Kinematic = true;
        body.Velocity = Vector2.Zero;
        body.Position = new Vector2(EdgeClimbX(_web), body.Position.Y);
        State = SpiderState.EdgeClimb;
        return true;
    }

    // По краю экрана вверх до высоты места на паутине.
    private void EdgeClimb(float dt, Physics body)
    {
        if (Webs is not { Enabled: true } || _web is null)
        {
            Drop(body);
            return;
        }

        Hold = new Hold(Footing.Wall, WallX: _web.Origin.X, WallSide: -_web.Away);
        float y = body.Position.Y - Tuning.EdgeClimbSpeed * dt;
        if (y <= SitPoint(_web).Y)
        {
            y = SitPoint(_web).Y;
            State = SpiderState.WebWalk;
        }

        body.Position = new Vector2(body.Position.X, y);
    }

    // С края экрана — по паутине (или будущей паутине) к месту. Дошёл: плетёт, если ещё не сплетена.
    private void WebWalk(float dt, Physics body)
    {
        if (Webs is not { Enabled: true } webs || _web is null)
        {
            Drop(body);
            return;
        }

        Hold = new Hold(Footing.Spread);
        Vector2 sit = SitPoint(_web);
        float dx = sit.X - body.Position.X;
        if (Math.Abs(dx) > Tuning.WalkSpeed * dt)
        {
            body.Position += new Vector2(Math.Sign(dx) * Tuning.WalkSpeed * dt, 0);
            return;
        }

        body.Position = sit;
        _web = webs.At(_web.Corner) ?? webs.Begin(_web.Corner);
        _webStayLeft = Between(Tuning.WebStayMinSeconds, Tuning.WebStayMaxSeconds);
        if (_web.Finished)
        {
            Sit();
        }
        else
        {
            State = SpiderState.Weave;
            _webPhase = 0;
        }
    }

    // Плетёт: снуёт вокруг места, паутина прибавляется нить за нитью.
    private void Weave(float dt, Physics body)
    {
        if (WebLost())
        {
            Drop(body);
            return;
        }

        Hold = new Hold(Footing.Spread);
        _web!.Weave(dt);
        _webPhase += dt;
        Vector2 around = new(MathF.Cos(_webPhase * 3) * 18, MathF.Sin(_webPhase * 3) * 10);
        body.Position = SitPoint(_web) + around;
        if (_web.Finished)
        {
            Sit();
        }
    }

    private void OnWeb(float dt, Physics body, IReadOnlyList<Surface> surfaces)
    {
        if (WebLost())
        {
            Drop(body);
            return;
        }

        Hold = new Hold(Footing.Spread);
        body.Position = SitPoint(_web!);
        _secondsLeft -= dt;
        _webStayLeft -= dt;
        if (_secondsLeft > 0)
        {
            return;
        }

        if (_webStayLeft <= 0)
        {
            StartLeave(body, surfaces);
        }
        else if (Roll() < Tuning.DescendChance)
        {
            StartDescend(surfaces);
        }
        else
        {
            _secondsLeft = Between(Tuning.WebSitMinSeconds, Tuning.WebSitMaxSeconds);
        }
    }

    private void OnThread(float dt, Physics body, IReadOnlyList<Surface> surfaces)
    {
        if (WebLost() || ThreadCut(body))
        {
            Drop(body);
            return;
        }

        Hold = new Hold(Footing.Air);
        Vector2 seat = _web!.Seat;
        float sitY = SitPoint(_web).Y;
        float y = body.Position.Y;
        switch (State)
        {
            case SpiderState.Descend:
                y = Math.Min(y + Tuning.DescendSpeed * dt, _threadTargetY);
                if (y >= _threadTargetY)
                {
                    State = SpiderState.Dangle;
                    _secondsLeft = Between(Tuning.DangleMinSeconds, Tuning.DangleMaxSeconds);
                }

                body.Position = new Vector2(seat.X, y);
                return;
            case SpiderState.Dangle:
                _webPhase += dt;
                body.Position = new Vector2(seat.X + MathF.Sin(_webPhase * 2) * 4, y);
                _secondsLeft -= dt;
                if (_secondsLeft <= 0)
                {
                    State = SpiderState.Ascend;
                }

                return;
            case SpiderState.Ascend:
                y -= Tuning.AscendSpeed * dt;
                body.Position = new Vector2(seat.X, Math.Max(y, sitY));
                if (y <= sitY)
                {
                    Sit();
                }

                return;
            case SpiderState.WebLeave:
                y += Tuning.DescendSpeed * dt;
                if (y >= _threadTargetY && SurfaceBelow(seat.X, sitY, surfaces) is Surface floor)
                {
                    body.PlaceOn(new Vector2(seat.X, floor.Top));
                    _web = null;
                    Settle(floor);
                    return;
                }

                body.Position = new Vector2(seat.X, y);
                return;
        }
    }

    private void Sit()
    {
        State = SpiderState.OnWeb;
        _secondsLeft = Between(Tuning.WebSitMinSeconds, Tuning.WebSitMaxSeconds);
    }

    // Спуск на случайную глубину, но не до самой опоры под паутиной.
    private void StartDescend(IReadOnlyList<Surface> surfaces)
    {
        float sitY = SitPoint(_web!).Y;
        float target = sitY + Between(Tuning.DescendMin, Tuning.DescendMax);
        if (SurfaceBelow(_web!.Seat.X, sitY, surfaces) is Surface below)
        {
            target = Math.Min(target, below.Top - Tuning.PounceOffsetY);
        }

        if (target <= sitY + Tuning.ClimbMinHeight)
        {
            _secondsLeft = Between(Tuning.WebSitMinSeconds, Tuning.WebSitMaxSeconds);
            return;
        }

        _threadTargetY = target;
        State = SpiderState.Descend;
    }

    // Насиделся — спускается по нити до опоры под паутиной; паутина остаётся.
    private void StartLeave(Physics body, IReadOnlyList<Surface> surfaces)
    {
        if (SurfaceBelow(_web!.Seat.X, SitPoint(_web).Y, surfaces) is not Surface below)
        {
            Drop(body);
            return;
        }

        _threadTargetY = below.Top;
        State = SpiderState.WebLeave;
    }

    // Курсор прошёл поперёк нити — нить порвана.
    private bool ThreadCut(Physics body)
    {
        Vector2? last = _lastCursorOnThread;
        _lastCursorOnThread = Cursor;
        if (Cursor is not Vector2 now || last is not Vector2 before || now == before)
        {
            return false;
        }

        Vector2 headTop = body.Position - new Vector2(0, Tuning.WebSitDrop * 2);
        return headTop.Y > _web!.Seat.Y && Web.SegmentDistance(_web.Seat, headTop, before, now) <= Tuning.WipeRadius;
    }

    private bool WebLost() => _web is null || Webs is not { Enabled: true } webs || !webs.Has(_web);

    private void Drop(Physics body)
    {
        body.Detach();
        State = SpiderState.Fall;
        Hold = new Hold(Footing.Air);
        _web = null;
    }

    private static Vector2 SitPoint(Web web) => web.Seat + new Vector2(0, Tuning.WebSitDrop);

    // Тело — у края экрана с той стороны, где угол паутины.
    private static float EdgeClimbX(Web web) => web.Origin.X + web.Away * Tuning.ClimbBodyOffset;

    private static Surface? SurfaceBelow(float x, float y, IReadOnlyList<Surface> surfaces)
    {
        Surface? best = null;
        foreach (Surface surface in surfaces)
        {
            if (surface.SpansX(x) && surface.Top > y && (best is null || surface.Top < best.Value.Top))
            {
                best = surface;
            }
        }

        return best;
    }
}
