using System.Numerics;
using SpiderWidget.Targets;

namespace SpiderWidget.Spider;

/// Тело паука. Position — точка, на которой он стоит (низ по центру).
internal sealed class Physics
{
    private const float OnEdgeTolerance = 0.5f;
    private const float LostBelow = 1000f;

    public Vector2 Position { get; set; }

    public Vector2 Velocity { get; set; }

    public bool Grounded { get; private set; }

    /// Телом управляет SpiderBrain напрямую (лезет по стене, соскальзывает) — гравитации нет.
    public bool Kinematic { get; set; }

    /// Где может быть тело по горизонтали (экран); null — без границ.
    public RectangleF? World { get; set; }

    /// Поставить тело на кромку: конец подъёма.
    public void PlaceOn(Vector2 point)
    {
        Position = point;
        Velocity = Vector2.Zero;
        Kinematic = false;
        Grounded = true;
    }

    /// Отпустить опору: дальше тело падает. Вызывать, когда тело уже чуть ниже кромки,
    /// иначе Step сразу поставит его обратно.
    public void Detach()
    {
        Velocity = Vector2.Zero;
        Kinematic = false;
        Grounded = false;
    }

    public void Step(float dt, IReadOnlyList<Surface> surfaces)
    {
        if (Kinematic)
        {
            return;
        }

        if (Grounded)
        {
            // На опоре движемся только по горизонтали — скорость по X задаёт SpiderBrain.
            Position += new Vector2(Velocity.X * dt, 0);
            Velocity = new Vector2(Velocity.X, 0);
            if (FindSupport(surfaces) is not null)
            {
                return;
            }
        }

        // Опора исчезла или тело в воздухе — падаем.
        Grounded = false;
        Vector2 velocity = Velocity + new Vector2(0, Tuning.Gravity * dt);
        velocity.Y = Math.Min(velocity.Y, Tuning.MaxFallSpeed);

        Vector2 from = Position;
        Vector2 to = from + velocity * dt;
        if (World is RectangleF world && (to.X < world.Left || to.X > world.Right))
        {
            to.X = Math.Clamp(to.X, world.Left, world.Right); // края экрана — стены: брошенный паук не улетает
            velocity.X = 0;
        }

        if (velocity.Y > 0 && FindLanding(from, to, surfaces) is Vector2 landing)
        {
            Position = landing;
            Velocity = Vector2.Zero;
            Grounded = true;
            return;
        }

        Position = to;
        Velocity = velocity;

        // Пол есть только под мониторами; если тело провалилось туда, где его нет, — роняем сверху заново.
        if (World is RectangleF bounds && to.Y > bounds.Bottom + LostBelow)
        {
            Position = new Vector2(bounds.Left + bounds.Width / 2, bounds.Top);
            Velocity = Vector2.Zero;
        }
    }

    /// Список опор обновился. Если тело стоит на окне, а окно сдвинулось, — сдвигаем тело вместе с ним.
    /// Если в новом месте опоры под телом нет (кромку закрыли), следующий Step уронит его.
    public void Carry(IReadOnlyList<Surface> before, IReadOnlyList<Surface> after)
    {
        if (!Grounded || Kinematic || FindSupport(before) is not Surface support || support.Owner == 0)
        {
            return;
        }

        foreach (Surface surface in after)
        {
            if (surface.Owner == support.Owner)
            {
                Position += new Vector2(surface.OwnerOrigin.X - support.OwnerOrigin.X, surface.OwnerOrigin.Y - support.OwnerOrigin.Y);
                return;
            }
        }
    }

    /// Опора, на кромке которой сейчас стоит тело, или null.
    public Surface? FindSupport(IReadOnlyList<Surface> surfaces)
    {
        foreach (Surface surface in surfaces)
        {
            if (Math.Abs(Position.Y - surface.Top) <= OnEdgeTolerance && surface.SpansX(Position.X))
            {
                return surface;
            }
        }

        return null;
    }

    // Ищем первую кромку, которую пересекает отрезок from→to. Проверка по отрезку,
    // а не по конечной точке: иначе на большой скорости тело проскочит тонкую опору.
    private static Vector2? FindLanding(Vector2 from, Vector2 to, IReadOnlyList<Surface> surfaces)
    {
        Vector2? best = null;
        foreach (Surface surface in surfaces)
        {
            if (from.Y > surface.Top || to.Y < surface.Top)
            {
                continue;
            }

            float t = to.Y == from.Y ? 0 : (surface.Top - from.Y) / (to.Y - from.Y);
            float x = from.X + (to.X - from.X) * t;
            if (surface.SpansX(x) && (best is null || surface.Top < best.Value.Y))
            {
                best = new Vector2(x, surface.Top);
            }
        }

        return best;
    }
}
