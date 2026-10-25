using System.Numerics;
using SpiderWidget.Native;
using SpiderWidget.Spider;

namespace SpiderWidget.Overlay;

/// Окно поверх всех, через которое клики полностью проходят (WS_EX_TRANSPARENT):
/// паутина и нить только видны, нажимать сквозь них можно как обычно.
internal abstract class ClickThroughForm : Form
{
    protected ClickThroughForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= User32.WS_EX_LAYERED | User32.WS_EX_TRANSPARENT | User32.WS_EX_TOOLWINDOW | User32.WS_EX_NOACTIVATE;
            return cp;
        }
    }
}

/// Одна паутина в углу. Перерисовывается, только когда паутина изменилась (сплелась ещё
/// нить или курсор порвал несколько).
internal sealed class WebForm : ClickThroughForm
{
    private readonly int _scale;
    private LayeredCanvas? _canvas;
    private int _drawnVersion = -1;

    public WebForm(Web web, int scale)
    {
        Web = web;
        _scale = scale;
        Rectangle bounds = Rectangle.Round(web.Bounds);
        bounds.Inflate(scale, scale); // чтобы нити по самому краю влезли целиком
        Bounds = bounds;
    }

    public Web Web { get; }

    public void RedrawIfChanged()
    {
        if (Web.Version != _drawnVersion)
        {
            Redraw();
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        _canvas = new LayeredCanvas(Width, Height);
        Redraw();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _canvas?.Dispose();
        }

        base.Dispose(disposing);
    }

    private void Redraw()
    {
        if (_canvas is null)
        {
            return;
        }

        _canvas.Graphics.Clear(Color.Transparent);
        for (int i = 0; i < Web.Strands.Count; i++)
        {
            if (Web.IsShown(i))
            {
                Strand strand = Web.Strands[i];
                Sprites.DrawLine(_canvas.Graphics, ToCanvas(strand.A), ToCanvas(strand.B), _scale, Sprites.WebBrush);
            }
        }

        _canvas.Present(Handle, Location, Size);
        _drawnVersion = Web.Version;
    }

    private Point ToCanvas(Vector2 screen) => new(
        Math.Clamp((int)MathF.Floor((screen.X - Left) / _scale), 0, Width / _scale - 1),
        Math.Clamp((int)MathF.Floor((screen.Y - Top) / _scale), 0, Height / _scale - 1));
}

/// Нить, на которой паук висит под паутиной: от места на паутине до макушки.
internal sealed class ThreadForm : ClickThroughForm
{
    private const int MaxWidth = 60;

    private readonly int _scale;
    private LayeredCanvas? _canvas;

    public ThreadForm(int scale)
    {
        _scale = scale;
        Bounds = new Rectangle(0, 0, MaxWidth, SystemInformation.VirtualScreen.Height);
    }

    public void ShowThread(Vector2 from, Vector2 to)
    {
        if (!Visible)
        {
            Show();
        }

        if (_canvas is null)
        {
            return;
        }

        int left = (int)MathF.Min(from.X, to.X) - 2 * _scale;
        int top = (int)from.Y;
        var origin = new Point(left, top);
        var size = new Size(Math.Min((int)MathF.Abs(to.X - from.X) + 4 * _scale, MaxWidth), Math.Min((int)(to.Y - from.Y) + _scale, _canvas.Height));

        _canvas.Graphics.Clear(Color.Transparent);
        Sprites.DrawLine(_canvas.Graphics, ToCanvas(from, origin), ToCanvas(to, origin), _scale, Sprites.WebBrush);
        _canvas.Present(Handle, origin, size);
    }

    public void HideThread()
    {
        if (Visible)
        {
            Hide();
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        _canvas = new LayeredCanvas(Width, Height);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _canvas?.Dispose();
        }

        base.Dispose(disposing);
    }

    private Point ToCanvas(Vector2 screen, Point origin) => new(
        (int)MathF.Floor((screen.X - origin.X) / _scale),
        (int)MathF.Floor((screen.Y - origin.Y) / _scale));
}
