namespace SpiderWidget.Spider;

/// Все числа, которыми настраивается поведение паука. Единицы — физические пиксели и секунды.
internal static class Tuning
{
    public const float StepSeconds = 1f / 60f;
    public const float MaxCatchUpSeconds = 0.25f; // после сна/фриза не прокручиваем физику дольше этого
    public const float Gravity = 2400f;
    public const float MaxFallSpeed = 3000f;

    public const float WalkSpeed = 60f;
    public const float WalkMinSeconds = 1.5f;
    public const float WalkMaxSeconds = 5f;
    public const float IdleMinSeconds = 0.5f;
    public const float IdleMaxSeconds = 3f;
    // Лазание: к опорам выше (иконки, кромки окон). До грани паук доходит пешком, не прыгает.
    public const float ClimbChance = 0.6f;     // доля пауз, после которых он идёт лезть, если есть куда
    public const float ClimbRadius = 900f;     // по горизонтали от паука до грани опоры; иконки обычно у края экрана
    public const float ClimbMaxHeight = 400f;  // насколько выше может быть опора
    public const float ClimbMinHeight = 20f;   // ниже — это не подъём
    public const float ClimbSpeed = 80f;
    public const float ClimbBodyOffset = 30f;  // центр тела от стены, по которой лезет
    public const float WallSideTolerance = 10f; // грань может быть чуть за краем текущей опоры (иконки в столбик)

    // Висение на иконке и срыв.
    public const float SlipChancePerSecond = 0.03f;
    public const float HangMinSeconds = 3f;    // потом пробует забраться выше, если есть куда
    public const float HangMaxSeconds = 10f;
    public const float SlipSeconds = 0.35f;    // соскальзывает, дёргаясь, прежде чем упасть
    public const float SlipSlideSpeed = 30f;
    public const float LandSeconds = 0.3f;     // после падения приходит в себя

    // Еда: файлы, брошенные на паука.
    public const float EatSeconds = 1.2f;    // жуёт
    public const float ChewPeriod = 0.15f;   // рот открыт/закрыт по очереди
    public const float RefuseSeconds = 0.8f; // мотает головой
    public const int RefuseShake = 2;        // на сколько логических пикселей

    // Бросок мышью.
    public const float MaxThrowSpeed = 2500f;

    // Охота за курсором: подкрасться, присесть, прыгнуть в точку, где курсор был в момент прыжка.
    public const float HuntRadius = 300f;          // от головы паука до курсора
    public const float HuntChancePerSecond = 0.05f;
    public const float HuntCooldownSeconds = 20f;  // не чаще одной охоты за это время
    public const float SneakSpeed = 30f;
    public const float SneakMinSeconds = 0.8f;
    public const float SneakMaxSeconds = 1.8f;
    public const float CrouchSeconds = 0.35f;
    public const float JumpSpeed = 900f;           // средняя скорость прыжка; из неё — время полёта
    public const float JumpMinSeconds = 0.35f;
    public const float JumpMaxSeconds = 0.7f;
    public const float PounceSeconds = 0.3f;       // «схватил» курсор и падает
    public const float PounceOffsetY = 40f;        // точка опоры тела — чуть ниже курсора, чтобы курсор был у головы

    public const float EdgeMargin = 54f; // центр тела не ближе этого к краю опоры, чтобы ноги не висели в воздухе (крайняя ступня — 18 лог. px × 3)
}
