using System.Numerics;
using SpiderWidget.Targets;

namespace SpiderWidget.Spider;

internal enum SpiderState
{
    Idle,
    Walk,
    Approach, // идёт к подножию грани, по которой полезет
    Climb,    // лезет вверх по грани
    Mantle,   // перелезает через кромку наверх
    Hang,     // сидит на иконке, вцепившись
    Slip,     // соскальзывает с иконки
    Fall,
    Land,
    Grabbed,  // держат мышью
    Thrown,   // летит после броска
    Hunt,     // заметил курсор, подкрадывается
    Crouch,   // присел перед прыжком
    Jump,     // летит к курсору
    Pounce,   // «схватил» и падает
    WebApproach, // бежит к краю экрана под углом, где будет (или есть) паутина
    EdgeClimb,   // лезет по краю экрана наверх
    WebWalk,     // переходит с края экрана на место на паутине
    Weave,       // плетёт паутину
    OnWeb,       // сидит на паутине
    Descend,     // спускается на нити
    Dangle,      // висит на нити, покачиваясь
    Ascend,      // поднимается по нити обратно на паутину
    WebLeave,    // спускается по нити до пола и уходит гулять
}

/// Решает, что паук делает. На опоре управляет горизонтальной скоростью тела; при подъёме,
/// соскальзывании и на паутине ведёт тело сам (Physics.Kinematic); в падении не вмешивается.
/// Как стоят ноги — отдаёт в Hold. Паутина — в SpiderBrain.Web.cs.
internal sealed partial class SpiderBrain(Random random)
{
    private float _secondsLeft;
    private float _slipSeconds;
    private float _slipStartX;
    private Surface _target;
    private float _wallX;
    private int _wallSide;
    private float _approachX;
    private float _huntCooldown;
    private int _jumpStepsLeft;
    private bool _gaping;
    private float _eatSeconds;
    private float _refuseSeconds;

    public SpiderState State { get; private set; } = SpiderState.Idle;

    public int Facing { get; private set; } = 1;

    public Hold Hold { get; private set; } = new(Footing.Ground);

    /// Где курсор (экранные физические пиксели); null — не охотиться.
    public Vector2? Cursor { get; set; }

    /// Скорость прыжка из from в to и число шагов физики до цели. Считается под ту же схему,
    /// что в Physics.Step (сначала скорость += g·dt, потом позиция += скорость·dt), поэтому
    /// через steps шагов тело ровно в to, если по пути нет опор.
    public static (Vector2 Velocity, int Steps) JumpVelocity(Vector2 from, Vector2 to, float gravity, float dt)
    {
        float seconds = Math.Clamp(Vector2.Distance(from, to) / Tuning.JumpSpeed, Tuning.JumpMinSeconds, Tuning.JumpMaxSeconds);
        int steps = Math.Max(1, (int)MathF.Round(seconds / dt));
        float flight = steps * dt;
        float vx = (to.X - from.X) / flight;
        float vy = (to.Y - from.Y - gravity * dt * dt * steps * (steps + 1) / 2) / flight;
        return (new Vector2(vx, vy), steps);
    }

    /// Схватили мышью: что бы ни делал — бросает и дрыгает ногами. Положение тела ведёт окно.
    public void Grab(Physics body)
    {
        State = SpiderState.Grabbed;
        Hold = new Hold(Footing.Flail);
        body.Kinematic = true;
        body.Velocity = Vector2.Zero;
    }

    /// Над пауком тащат файлы: открывает рот и замирает (если стоит). false — унесли мимо.
    public void Gape(bool gaping) => _gaping = gaping;

    /// Бросили файлы: что-то ушло в корзину — жуёт; всё отклонено — мотает головой.
    public void Fed(bool ateSomething)
    {
        _gaping = false;
        if (ateSomething)
        {
            _eatSeconds = Tuning.EatSeconds;
        }
        else
        {
            _refuseSeconds = Tuning.RefuseSeconds;
        }
    }

    /// Занят едой: стоит на месте, пока не дожуёт или не домотает головой.
    public bool Busy => _gaping || _eatSeconds > 0 || _refuseSeconds > 0;

    /// Отпустили: летит с этой скоростью (нулевой — просто падает с места).
    public void Release(Physics body, Vector2 velocity)
    {
        body.Detach();
        body.Velocity = velocity;
        State = SpiderState.Thrown;
        Hold = new Hold(Footing.Air);
    }

    public void Step(float dt, Physics body, IReadOnlyList<Surface> surfaces)
    {
        StepBody(dt, body, surfaces);
        Hold = WithFace(dt, Hold);
    }

    // Лицо поверх любой позы: рот открыт, пока ждёт еду; жуёт — открывает и закрывает; отказ — мотает головой.
    private Hold WithFace(float dt, Hold hold)
    {
        _eatSeconds = Math.Max(0, _eatSeconds - dt);
        _refuseSeconds = Math.Max(0, _refuseSeconds - dt);
        bool chewOpen = _eatSeconds > 0 && (int)(_eatSeconds / Tuning.ChewPeriod) % 2 == 0;
        int shake = _refuseSeconds > 0 ? (int)MathF.Round(MathF.Sin(_refuseSeconds * 30) * Tuning.RefuseShake) : 0;
        return hold with { MouthOpen = _gaping || chewOpen, HeadShift = shake };
    }

    private void StepBody(float dt, Physics body, IReadOnlyList<Surface> surfaces)
    {
        _huntCooldown -= dt;
        if (StepWeb(dt, body, surfaces))
        {
            return;
        }

        switch (State)
        {
            case SpiderState.Grabbed:
                return;
            case SpiderState.Climb:
                Climb(dt, body);
                return;
            case SpiderState.Mantle:
                Mantle(dt, body);
                return;
            case SpiderState.Slip:
                Slip(dt, body);
                return;
        }

        if (!body.Grounded)
        {
            Airborne(dt);
            return;
        }

        if (body.FindSupport(surfaces) is not Surface support)
        {
            return;
        }

        Hold = support.Kind == SurfaceKind.Icon ? GripOn(support) : new Hold(Footing.Ground);
        if (Busy && State is SpiderState.Idle or SpiderState.Walk or SpiderState.Hang or SpiderState.Hunt or SpiderState.Approach)
        {
            Stop(body); // ест — не гуляет, не охотится, не лезет; таймеры поведения стоят
            return;
        }

        _secondsLeft -= dt;
        if (State is SpiderState.Idle or SpiderState.Walk or SpiderState.Hang && TryStartHunt(dt, body))
        {
            return;
        }

        switch (State)
        {
            case SpiderState.Hunt:
                Hunt(dt, body, support);
                return;
            case SpiderState.Crouch:
                Stop(body);
                Hold = Hold with { Crouch = true };
                if (_secondsLeft <= 0)
                {
                    StartJump(body);
                }

                return;
            case SpiderState.Fall or SpiderState.Thrown or SpiderState.Jump or SpiderState.Pounce:
                State = SpiderState.Land;
                _secondsLeft = Tuning.LandSeconds;
                Stop(body);
                return;
            case SpiderState.Land:
                Stop(body);
                if (_secondsLeft <= 0)
                {
                    Settle(support);
                }

                return;
            case SpiderState.Approach:
                Approach(dt, body);
                return;
            case SpiderState.Hang:
                Hang(dt, body, support, surfaces);
                return;
        }

        if (support.Kind == SurfaceKind.Icon)
        {
            Settle(support);
            Stop(body);
            return;
        }

        Wander(dt, body, support, surfaces);
    }

    // В воздухе: летит после броска или прыжка, «хватает» курсор в конце прыжка, иначе просто падает.
    private void Airborne(float dt)
    {
        Hold = new Hold(Footing.Air);
        switch (State)
        {
            case SpiderState.Thrown:
                return;
            case SpiderState.Jump:
                if (--_jumpStepsLeft <= 0)
                {
                    State = SpiderState.Pounce;
                    _secondsLeft = Tuning.PounceSeconds;
                    Hold = new Hold(Footing.Flail);
                }

                return;
            case SpiderState.Pounce:
                Hold = new Hold(Footing.Flail);
                _secondsLeft -= dt;
                if (_secondsLeft <= 0)
                {
                    State = SpiderState.Fall;
                }

                return;
            default:
                State = SpiderState.Fall;
                return;
        }
    }

    // Курсор рядом, кулдаун прошёл, и повезло — начинает охоту. Курсор читается опросом,
    // глобальных хуков мыши нет.
    private bool TryStartHunt(float dt, Physics body)
    {
        if (Cursor is not Vector2 cursor || _huntCooldown > 0 || Vector2.Distance(Head(body), cursor) > Tuning.HuntRadius)
        {
            return false;
        }

        if (random.NextDouble() >= ChanceIn(Tuning.HuntChancePerSecond, dt))
        {
            return false;
        }

        State = SpiderState.Hunt;
        _secondsLeft = Between(Tuning.SneakMinSeconds, Tuning.SneakMaxSeconds);
        _huntCooldown = Tuning.HuntCooldownSeconds;
        return true;
    }

    // Подкрадывается к курсору вдоль опоры, медленно; курсор ушёл далеко — бросает охоту.
    private void Hunt(float dt, Physics body, Surface support)
    {
        if (Cursor is not Vector2 cursor || Vector2.Distance(Head(body), cursor) > Tuning.HuntRadius * 1.5f)
        {
            Settle(support);
            return;
        }

        float minX = support.Bounds.Left + Tuning.EdgeMargin;
        float maxX = support.Bounds.Right - Tuning.EdgeMargin;
        float speed = 0;
        if (Math.Abs(cursor.X - body.Position.X) > Tuning.SneakSpeed * dt && minX < maxX)
        {
            Facing = Math.Sign(cursor.X - body.Position.X);
            float nextX = body.Position.X + Facing * Tuning.SneakSpeed * dt;
            speed = nextX >= minX && nextX <= maxX ? Facing * Tuning.SneakSpeed : 0;
        }

        body.Velocity = new Vector2(speed, body.Velocity.Y);
        if (_secondsLeft <= 0)
        {
            State = SpiderState.Crouch;
            _secondsLeft = Tuning.CrouchSeconds;
            Stop(body);
        }
    }

    // Прыжок в точку, где курсор сейчас; дальше курсор не преследует.
    private void StartJump(Physics body)
    {
        Vector2 cursor = Cursor ?? Head(body);
        Vector2 target = cursor + new Vector2(0, Tuning.PounceOffsetY);
        if (target.Y > body.Position.Y)
        {
            // Прыжок вниз: стартуем на пиксель ниже кромки, иначе физика тут же поставит обратно.
            body.Position += new Vector2(0, 1);
        }

        (Vector2 velocity, int steps) = JumpVelocity(body.Position, target, Tuning.Gravity, Tuning.StepSeconds);
        body.Detach();
        body.Velocity = velocity;
        _jumpStepsLeft = steps;
        State = SpiderState.Jump;
        Hold = new Hold(Footing.Air);
    }

    private static Vector2 Head(Physics body) => body.Position - new Vector2(0, Tuning.PounceOffsetY);

    private void Wander(float dt, Physics body, Surface support, IReadOnlyList<Surface> surfaces)
    {
        if (_secondsLeft <= 0)
        {
            if (State == SpiderState.Idle && TryStartWebTrip(body, support))
            {
                return;
            }

            if (State == SpiderState.Idle && random.NextDouble() < Tuning.ClimbChance && TryStartClimb(body, support, surfaces))
            {
                return;
            }

            ToggleIdleWalk();
        }

        float minX = support.Bounds.Left + Tuning.EdgeMargin;
        float maxX = support.Bounds.Right - Tuning.EdgeMargin;
        if (State == SpiderState.Walk && minX >= maxX)
        {
            State = SpiderState.Idle; // опора уже, чем расставленные ноги, — гулять негде
        }

        if (State == SpiderState.Walk)
        {
            float nextX = body.Position.X + Facing * Tuning.WalkSpeed * dt;

            // Разворачиваемся, только если идём наружу: иначе паук, оказавшийся у края, дёргался бы на месте.
            if ((nextX < minX && Facing < 0) || (nextX > maxX && Facing > 0))
            {
                Facing = -Facing;
            }
        }

        float speed = State == SpiderState.Walk ? Facing * Tuning.WalkSpeed : 0;
        body.Velocity = new Vector2(speed, body.Velocity.Y);
    }

    private void Hang(float dt, Physics body, Surface support, IReadOnlyList<Surface> surfaces)
    {
        Stop(body);
        if (random.NextDouble() < ChanceIn(Tuning.SlipChancePerSecond, dt))
        {
            StartSlip(body);
            return;
        }

        if (_secondsLeft <= 0 && !TryStartClimb(body, support, surfaces))
        {
            _secondsLeft = Between(Tuning.HangMinSeconds, Tuning.HangMaxSeconds);
        }
    }

    // Подъём — на опору выше в пределах радиуса, по ближней к пауку боковой грани.
    private bool TryStartClimb(Physics body, Surface support, IReadOnlyList<Surface> surfaces)
    {
        var candidates = new List<(Surface Target, float WallX)>();
        foreach (Surface surface in surfaces)
        {
            if (surface.Kind is not (SurfaceKind.Icon or SurfaceKind.Window))
            {
                continue;
            }

            float height = body.Position.Y - surface.Top;
            if (height < Tuning.ClimbMinHeight || height > Tuning.ClimbMaxHeight)
            {
                continue;
            }

            float left = surface.Bounds.Left - 1;
            float right = surface.Bounds.Right + 1;
            float wallX = Math.Abs(left - body.Position.X) <= Math.Abs(right - body.Position.X) ? left : right;
            bool reachable = Math.Abs(wallX - body.Position.X) <= Tuning.ClimbRadius
                && wallX >= support.Bounds.Left - Tuning.WallSideTolerance
                && wallX <= support.Bounds.Right + Tuning.WallSideTolerance;
            if (reachable)
            {
                candidates.Add((surface, wallX));
            }
        }

        if (candidates.Count == 0)
        {
            return false;
        }

        (_target, _wallX) = candidates[random.Next(candidates.Count)];
        _wallSide = _wallX <= _target.Bounds.Left ? 1 : -1; // стена справа от тела, если лезем по левой грани
        _approachX = Math.Clamp(_wallX - _wallSide * Tuning.ClimbBodyOffset, support.Bounds.Left, support.Bounds.Right);
        State = SpiderState.Approach;
        return true;
    }

    private void Approach(float dt, Physics body)
    {
        float dx = _approachX - body.Position.X;
        if (Math.Abs(dx) > Tuning.WalkSpeed * dt)
        {
            Facing = Math.Sign(dx);
            body.Velocity = new Vector2(Facing * Tuning.WalkSpeed, 0);
            return;
        }

        body.Kinematic = true;
        body.Velocity = Vector2.Zero;
        body.Position = new Vector2(_wallX - _wallSide * Tuning.ClimbBodyOffset, body.Position.Y);
        State = SpiderState.Climb;
        Hold = WallHold();
    }

    private void Climb(float dt, Physics body)
    {
        Hold = WallHold();
        float y = body.Position.Y - Tuning.ClimbSpeed * dt;
        if (y <= _target.Top)
        {
            y = _target.Top;
            State = SpiderState.Mantle;
        }

        body.Position = new Vector2(body.Position.X, y);
    }

    // Наверху переходит с грани на кромку: к центру иконки или чуть внутрь окна.
    private void Mantle(float dt, Physics body)
    {
        Hold = _target.Kind == SurfaceKind.Icon ? GripOn(_target) : new Hold(Footing.Ground);
        float targetX = _target.Kind == SurfaceKind.Icon || _target.Bounds.Width <= 2 * Tuning.EdgeMargin
            ? _target.Bounds.Left + _target.Bounds.Width / 2f
            : Math.Clamp(body.Position.X, _target.Bounds.Left + Tuning.EdgeMargin, _target.Bounds.Right - Tuning.EdgeMargin);
        float dx = targetX - body.Position.X;
        if (Math.Abs(dx) > Tuning.WalkSpeed * dt)
        {
            body.Position += new Vector2(Math.Sign(dx) * Tuning.WalkSpeed * dt, 0);
            return;
        }

        body.PlaceOn(new Vector2(targetX, _target.Top));
        Settle(_target);
    }

    private void StartSlip(Physics body)
    {
        State = SpiderState.Slip;
        Hold = new Hold(Footing.Air);
        _slipSeconds = 0;
        _slipStartX = body.Position.X;
        body.Kinematic = true;
        body.Velocity = Vector2.Zero;
    }

    // Съезжает вниз, дёргаясь из стороны в сторону, и срывается. К концу тело уже ниже
    // кромки, поэтому физика не поставит его обратно на ту же иконку.
    private void Slip(float dt, Physics body)
    {
        _slipSeconds += dt;
        float shake = MathF.Sin(_slipSeconds * 70) * 2;
        body.Position = new Vector2(_slipStartX + shake, body.Position.Y + Tuning.SlipSlideSpeed * dt);
        if (_slipSeconds >= Tuning.SlipSeconds)
        {
            body.Detach();
            State = SpiderState.Fall;
        }
    }

    private void Settle(Surface support)
    {
        if (support.Kind == SurfaceKind.Icon)
        {
            State = SpiderState.Hang;
            _secondsLeft = Between(Tuning.HangMinSeconds, Tuning.HangMaxSeconds);
        }
        else
        {
            State = SpiderState.Idle;
            _secondsLeft = Between(Tuning.IdleMinSeconds, Tuning.IdleMaxSeconds);
        }
    }

    private void ToggleIdleWalk()
    {
        if (State == SpiderState.Idle)
        {
            State = SpiderState.Walk;
            Facing = random.Next(2) == 0 ? -1 : 1;
            _secondsLeft = Between(Tuning.WalkMinSeconds, Tuning.WalkMaxSeconds);
        }
        else
        {
            State = SpiderState.Idle;
            _secondsLeft = Between(Tuning.IdleMinSeconds, Tuning.IdleMaxSeconds);
        }
    }

    private Hold WallHold() => new(Footing.Wall, WallX: _wallX, WallSide: _wallSide);

    private static Hold GripOn(Surface surface) => new(Footing.Grip, surface.Bounds.Left, surface.Bounds.Right);

    private static void Stop(Physics body) => body.Velocity = new Vector2(0, body.Velocity.Y);

    /// Вероятность события за шаг dt, чтобы за секунду она была perSecond.
    private static float ChanceIn(float perSecond, float dt) => 1f - MathF.Pow(1f - perSecond, dt);

    private float Between(float min, float max) => min + (float)random.NextDouble() * (max - min);

    private double Roll() => random.NextDouble();

    private int Pick(int count) => random.Next(count);
}
