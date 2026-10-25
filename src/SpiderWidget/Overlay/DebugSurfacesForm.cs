using SpiderWidget.Native;
using SpiderWidget.Targets;

namespace SpiderWidget.Overlay;

/// Отладка (флаг --debug-surfaces): прозрачное окно на весь экран, рисует кромки опор.
/// Клики целиком проходят насквозь (WS_EX_TRANSPARENT).
internal sealed class DebugSurfacesForm : Form
{
    private static readonly Dictionary<SurfaceKind, Pen> Pens = new()
    {
        [SurfaceKind.Floor] = new Pen(Color.Lime, 3),
        [SurfaceKind.Window] = new Pen(Color.DeepSkyBlue, 3),
        [SurfaceKind.Taskbar] = new Pen(Color.Orange, 3),
        [SurfaceKind.Icon] = new Pen(Color.Red, 3),
    };

    private IReadOnlyList<Surface> _surfaces = [];

    public DebugSurfacesForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Bounds = SystemInformation.VirtualScreen;
        BackColor = Color.Magenta;
        TransparencyKey = Color.Magenta;
        DoubleBuffered = true;
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= User32.WS_EX_TRANSPARENT | User32.WS_EX_TOOLWINDOW | User32.WS_EX_NOACTIVATE;
            return cp;
        }
    }

    public void ShowSurfaces(IReadOnlyList<Surface> surfaces)
    {
        _surfaces = surfaces;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        foreach (Surface surface in _surfaces)
        {
            int y = surface.Top - Top;
            e.Graphics.DrawLine(Pens[surface.Kind], surface.Bounds.Left - Left, y, surface.Bounds.Right - Left, y);
        }
    }
}
