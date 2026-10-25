using System.Diagnostics;
using System.Drawing.Imaging;
using System.Numerics;
using System.Runtime.InteropServices;
using SpiderWidget.Native;
using SpiderWidget.Recycle;
using SpiderWidget.Spider;
using SpiderWidget.Targets;

namespace SpiderWidget.Overlay;

/// Окно паука: размером с паука, поверх всех, не берёт фокус, нет в Alt-Tab.
/// Рисуется через UpdateLayeredWindow с попиксельной альфой: пиксели с
/// альфой 0 Windows сама пропускает насквозь при клике, в том числе в окна
/// других процессов (HTTRANSPARENT так не умеет — он работает только внутри потока).
internal sealed class SpiderForm : Form
{
    private const int PixelScale = 3;
    private const int CanvasWidth = 48; // в логических пикселях: голова 24 + ноги в стороны
    private const int CanvasHeight = 44;

    private readonly Physics _body = new();
    private HeadStyle _head;
    private Legs _legs;
    private readonly SpiderBrain _brain = new(new Random());
    private readonly Webs _webs = new(Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 800, 600));
    private readonly Dictionary<Web, WebForm> _webForms = [];
    private readonly ThreadForm _thread = new(PixelScale);
    private IReadOnlyList<Surface> _surfaces = [];
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 30 }; // ~30 кадров/с; физика всё равно шагает по 1/60 с
    private readonly System.Windows.Forms.Timer _surfaceTimer = new() { Interval = 500 }; // опрос окон 2 раза в секунду
    private readonly Stopwatch _clock = new();
    private float _pendingSeconds;
    private bool _wasAnimating;
    private Hold _lastHold;

    // Захват мышью: где держат паука относительно курсора и история курсора для броска.
    private readonly Stopwatch _uptime = Stopwatch.StartNew();
    private readonly PathRules _pathRules = PathRules.Current();
    private Grab? _grab;
    private Vector2 _grabOffset;

    // Положение окна ведём сами и двигаем окно тем же UpdateLayeredWindow, что и рисует, — один вызов на кадр.
    private Point _origin;

    // Холст живёт всё время работы: GDI+ рисует прямо в память DIB-секции, которую берёт UpdateLayeredWindow.
    private nint _memoryDc;
    private nint _dib;
    private nint _previousBitmap;
    private Bitmap? _canvas;
    private Graphics? _graphics;

    public SpiderForm(HeadStyle head)
    {
        _head = head;
        _legs = new Legs(PixelScale, head.Closed.Height);
        _brain.Webs = _webs;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Size = new Size(CanvasWidth * PixelScale, CanvasHeight * PixelScale);
        AllowDrop = true;

        RefreshSurfaces();

        // Края экрана — стены; чуть внутрь, чтобы паука было видно целиком хотя бы наполовину.
        RectangleF world = SystemInformation.VirtualScreen;
        world.Inflate(-Tuning.EdgeMargin, 0);
        _body.World = world;

        Rectangle area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 800, 600);
        _body.Position = new Vector2(area.Left + area.Width / 2f, area.Top + area.Height / 2f);
        _legs.Place(_body.Position);
        MoveToBody();
        Location = _origin;

        _timer.Tick += (_, _) => Tick();
        _surfaceTimer.Tick += (_, _) =>
        {
            RefreshSurfaces();
            KeepOnTop();
        };
    }

    /// Сменить голову на ходу. Ноги пересобираются под её высоту и сразу встают на место.
    public void SetHead(HeadStyle head)
    {
        _head = head;
        _legs = new Legs(PixelScale, head.Closed.Height);
        _legs.Place(_body.Position);
        Redraw();
    }

    /// Плести ли паутину. Выключили — все паутины исчезают, паук с паутины падает.
    public bool WebsEnabled
    {
        get => _webs.Enabled;
        set
        {
            _webs.Enabled = value;
            SyncWebWindows();
        }
    }

    /// Список опор обновился (для отладочной обводки).
    public event Action<IReadOnlyList<Surface>>? SurfacesRefreshed;

    public bool Paused
    {
        get => !_timer.Enabled;
        set
        {
            _timer.Enabled = !value;
            _surfaceTimer.Enabled = !value;
            _clock.Restart();
            _pendingSeconds = 0;
        }
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= User32.WS_EX_LAYERED | User32.WS_EX_TOOLWINDOW | User32.WS_EX_NOACTIVATE;
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        CreateCanvas();
        Redraw();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Paused = false;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
            _surfaceTimer.Dispose();
            foreach (WebForm form in _webForms.Values)
            {
                form.Dispose();
            }

            _thread.Dispose();
            _graphics?.Dispose();
            _canvas?.Dispose();
        }

        if (_memoryDc != 0)
        {
            Gdi32.SelectObject(_memoryDc, _previousBitmap);
            Gdi32.DeleteObject(_dib);
            Gdi32.DeleteDC(_memoryDc);
            _memoryDc = 0;
        }

        base.Dispose(disposing);
    }

    // Перетаскивание файлов на паука. Как и клик, его ловят только непрозрачные пиксели.
    // Эффект — Copy, а не Move: при Move Проводник после броска сам удалил бы исходные файлы
    // в обход фильтра путей. С Copy источник ничего не трогает, удаляет только RecycleBin.Eat.
    protected override void OnDragEnter(DragEventArgs e)
    {
        base.OnDragEnter(e);
        bool files = e.Data?.GetDataPresent(DataFormats.FileDrop) == true;
        e.Effect = files ? DragDropEffects.Copy : DragDropEffects.None;
        _brain.Gape(files);
    }

    protected override void OnDragOver(DragEventArgs e)
    {
        base.OnDragOver(e);
        e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true ? DragDropEffects.Copy : DragDropEffects.None;
    }

    protected override void OnDragLeave(EventArgs e)
    {
        base.OnDragLeave(e);
        _brain.Gape(false);
    }

    protected override void OnDragDrop(DragEventArgs e)
    {
        base.OnDragDrop(e);
        if (e.Data?.GetData(DataFormats.FileDrop) is not string[] paths || paths.Length == 0)
        {
            _brain.Gape(false);
            return;
        }

        // Удаляем уже после того, как Проводник получит ответ на бросок: иначе он ждал бы,
        // пока большая папка уезжает в корзину.
        BeginInvoke(() =>
        {
            (int recycled, _) = RecycleBin.Eat(paths, _pathRules, Handle);
            _brain.Fed(recycled > 0);
        });
    }

    // Клик ловят только непрозрачные пиксели (голова, ноги) — остальное layered-окно пропускает.
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || _grab != null)
        {
            return;
        }

        Vector2 cursor = CursorPosition();
        _grab = new Grab();
        _grab.Add(cursor, Now());
        _grabOffset = _body.Position - cursor;
        _brain.Grab(_body);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button == MouseButtons.Left)
        {
            Release();
        }
    }

    // Захват мыши потерян (Alt+Tab, Win и т. п.) — просто отпускаем.
    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        base.OnMouseCaptureChanged(e);
        Release();
    }

    private void Release()
    {
        if (_grab is null)
        {
            return;
        }

        _grab.Add(CursorPosition(), Now());
        _brain.Release(_body, _grab.ThrowVelocity());
        _grab = null;
    }

    // В процессе с PerMonitorV2 Cursor.Position — в тех же физических пикселях, что и опоры.
    private static Vector2 CursorPosition() => new(Cursor.Position.X, Cursor.Position.Y);

    private float Now() => (float)_uptime.Elapsed.TotalSeconds;

    // Таймер WinForms неточен, поэтому физику крутим фиксированными шагами по реальному времени.
    private void Tick()
    {
        Vector2 cursor = CursorPosition();
        _brain.Cursor = cursor;
        if (_grab != null)
        {
            _grab.Add(cursor, Now());
            _body.Position = cursor + _grabOffset;
        }

        _pendingSeconds = Math.Min(_pendingSeconds + (float)_clock.Elapsed.TotalSeconds, Tuning.MaxCatchUpSeconds);
        _clock.Restart();
        int steps = 0;
        while (_pendingSeconds >= Tuning.StepSeconds)
        {
            _brain.Step(Tuning.StepSeconds, _body, _surfaces);
            _body.Step(Tuning.StepSeconds, _surfaces);
            _legs.Update(Tuning.StepSeconds, _body.Position, _brain.Hold, _body.Velocity.X);
            _pendingSeconds -= Tuning.StepSeconds;
            steps++;
        }

        _webs.Update(steps * Tuning.StepSeconds, cursor); // тишина в углах и смахивание паутины курсором
        SyncWebWindows();
        UpdateThread();

        // Перерисовываем, пока что-то меняется, и ещё один кадр после конца шага — чтобы ступня встала на место.
        bool moved = MoveToBody();
        bool animating = _legs.Animating || _grab != null; // в руке дрыгает ногами, даже если курсор стоит
        bool poseChanged = _brain.Hold != _lastHold;     // например, присел перед прыжком
        if (moved || animating || _wasAnimating || poseChanged)
        {
            Redraw();
        }

        _wasAnimating = animating;
        _lastHold = _brain.Hold;
    }

    // У каждой паутины — своё окно: появилась — показать, порвана — закрыть, изменилась — перерисовать.
    private void SyncWebWindows()
    {
        foreach (Web gone in _webForms.Keys.Where(w => !_webs.Has(w)).ToList())
        {
            _webForms[gone].Close();
            _webForms[gone].Dispose();
            _webForms.Remove(gone);
        }

        foreach (Web web in _webs.All)
        {
            if (!_webForms.TryGetValue(web, out WebForm? form))
            {
                form = new WebForm(web, PixelScale);
                _webForms[web] = form;
                form.Show();
                KeepOnTop(); // новая паутина легла поверх паука — паука обратно наверх
            }

            form.RedrawIfChanged();
        }
    }

    // Нить от места на паутине до макушки паука, пока он на ней висит.
    private void UpdateThread()
    {
        float headTop = _legs.BodyCenter(_body.Position).Y - _head.Closed.Height / 2f * PixelScale;
        if (_brain.ThreadAnchor is Vector2 anchor && headTop > anchor.Y + PixelScale)
        {
            _thread.ShowThread(anchor, new Vector2(_body.Position.X, headTop));
            return;
        }

        _thread.HideThread();
    }

    // TopMost — общий слой: окно «поверх всех», активированное позже (уведомление, скриншотер,
    // панель задач), ложится выше паука. Поэтому регулярно поднимаемся наверх этого слоя.
    // Меню Пуск, экран блокировки и эксклюзивный полноэкранный режим так не перекрыть — и не нужно.
    private void KeepOnTop()
    {
        if (IsHandleCreated)
        {
            User32.SetWindowPos(Handle, User32.HWND_TOPMOST, 0, 0, 0, 0, User32.SWP_NOMOVE | User32.SWP_NOSIZE | User32.SWP_NOACTIVATE);
        }
    }

    // Опоры: пол каждого монитора + видимые кромки окон и панелей задач.
    private void RefreshSurfaces()
    {
        IReadOnlyList<Surface> next = [.. Surface.Floors(Screen.AllScreens.Select(s => s.WorkingArea)), .. WindowSurfaces.Collect()];
        Vector2 before = _body.Position;
        _body.Carry(_surfaces, next);
        _surfaces = next;
        SurfacesRefreshed?.Invoke(next);
        if (_webs.All.Count == 0 && Screen.PrimaryScreen is Screen primary)
        {
            _webs.Area = primary.WorkingArea; // экран поменялся — новые паутины по новым углам
        }
        if (_body.Position != before)
        {
            _legs.Place(_body.Position); // окно уехало — ноги переставляем сразу, а не шагом через полэкрана
        }
    }

    // Точка опоры тела — середина нижнего края окна. Возвращает true, если окно должно сдвинуться.
    private bool MoveToBody()
    {
        var origin = new Point((int)MathF.Round(_body.Position.X) - Width / 2, (int)MathF.Round(_body.Position.Y) - Height);
        if (origin == _origin)
        {
            return false;
        }

        _origin = origin;
        return true;
    }

    // Экранная точка → логический пиксель холста. По y берём пиксель над точкой:
    // ступня на кромке опоры должна рисоваться над ней, а не на ней.
    private Point ToCanvas(Vector2 screen) => new(
        (int)MathF.Floor((screen.X - _origin.X) / PixelScale),
        (int)MathF.Ceiling((screen.Y - _origin.Y) / PixelScale) - 1);

    private void CreateCanvas()
    {
        var header = new Gdi32.BITMAPINFOHEADER
        {
            Size = Marshal.SizeOf<Gdi32.BITMAPINFOHEADER>(),
            Width = Width,
            Height = -Height, // отрицательная высота — строки сверху вниз, как у Bitmap
            Planes = 1,
            BitCount = 32,
        };
        _memoryDc = Gdi32.CreateCompatibleDC(0);
        _dib = Gdi32.CreateDIBSection(_memoryDc, ref header, 0, out nint bits, 0, 0);
        _previousBitmap = Gdi32.SelectObject(_memoryDc, _dib);
        _canvas = new Bitmap(Width, Height, Width * 4, PixelFormat.Format32bppPArgb, bits);
        _graphics = Graphics.FromImage(_canvas);
    }

    private void Redraw()
    {
        if (_graphics is null)
        {
            return;
        }

        _graphics.Clear(Color.Transparent);

        // Сначала ноги: плечи спрятаны под головой.
        foreach (LegPose pose in _legs.Poses(_body.Position))
        {
            Point hip = ToCanvas(pose.Hip);
            Point knee = ToCanvas(pose.Knee);
            Sprites.DrawLine(_graphics, hip, knee, PixelScale);
            Sprites.DrawLine(_graphics, knee, ToCanvas(pose.Foot), PixelScale);
        }

        Point center = ToCanvas(_legs.BodyCenter(_body.Position));
        Hold hold = _brain.Hold;
        Sprite head = hold.MouthOpen ? _head.Open : _head.Closed;
        int headLeft = center.X - head.Width / 2 + hold.HeadShift;
        _graphics.DrawImageUnscaled(head.Rendered(PixelScale), headLeft * PixelScale, (center.Y - head.Height / 2) * PixelScale);
        _graphics.Flush(System.Drawing.Drawing2D.FlushIntention.Sync);

        var destination = new User32.POINT(_origin.X, _origin.Y);
        var size = new User32.SIZE(Width, Height);
        var source = new User32.POINT(0, 0);
        var blend = new User32.BLENDFUNCTION
        {
            BlendOp = User32.AC_SRC_OVER,
            SourceConstantAlpha = 255,
            AlphaFormat = User32.AC_SRC_ALPHA,
        };
        User32.UpdateLayeredWindow(
            Handle, 0, ref destination, ref size, _memoryDc, ref source, 0, ref blend, User32.ULW_ALPHA);
    }
}
