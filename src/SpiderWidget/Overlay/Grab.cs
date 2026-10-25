using System.Numerics;
using SpiderWidget.Spider;

namespace SpiderWidget.Overlay;

/// Пока паука держат мышью — помнит, где был курсор за последние WindowSeconds.
/// Скорость броска — средняя скорость курсора за это окно: рывок в конце решает, куда полетит.
internal sealed class Grab
{
    private const float WindowSeconds = 0.1f;

    private readonly Queue<(Vector2 Point, float Time)> _samples = new();

    public void Add(Vector2 point, float time)
    {
        _samples.Enqueue((point, time));
        while (_samples.Count > 1 && time - _samples.Peek().Time > WindowSeconds)
        {
            _samples.Dequeue();
        }
    }

    public Vector2 ThrowVelocity()
    {
        if (_samples.Count < 2)
        {
            return Vector2.Zero;
        }

        (Vector2 firstPoint, float firstTime) = _samples.Peek();
        (Vector2 lastPoint, float lastTime) = _samples.Last();
        float dt = lastTime - firstTime;
        if (dt <= 0)
        {
            return Vector2.Zero;
        }

        Vector2 velocity = (lastPoint - firstPoint) / dt;
        float speed = velocity.Length();
        return speed > Tuning.MaxThrowSpeed ? velocity / speed * Tuning.MaxThrowSpeed : velocity;
    }
}
