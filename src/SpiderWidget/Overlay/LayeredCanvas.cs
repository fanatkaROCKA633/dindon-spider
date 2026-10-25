using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using SpiderWidget.Native;

namespace SpiderWidget.Overlay;

/// Холст для layered-окна: GDI+ рисует прямо в память DIB-секции, UpdateLayeredWindow
/// показывает её с попиксельной альфой. Создаётся один раз под наибольший нужный размер.
internal sealed class LayeredCanvas : IDisposable
{
    private readonly nint _memoryDc;
    private readonly nint _dib;
    private readonly nint _previousBitmap;
    private readonly Bitmap _bitmap;

    public LayeredCanvas(int width, int height)
    {
        Width = width;
        Height = height;
        var header = new Gdi32.BITMAPINFOHEADER
        {
            Size = Marshal.SizeOf<Gdi32.BITMAPINFOHEADER>(),
            Width = width,
            Height = -height, // строки сверху вниз, как у Bitmap
            Planes = 1,
            BitCount = 32,
        };
        _memoryDc = Gdi32.CreateCompatibleDC(0);
        _dib = Gdi32.CreateDIBSection(_memoryDc, ref header, 0, out nint bits, 0, 0);
        _previousBitmap = Gdi32.SelectObject(_memoryDc, _dib);
        _bitmap = new Bitmap(width, height, width * 4, PixelFormat.Format32bppPArgb, bits);
        Graphics = Graphics.FromImage(_bitmap);
    }

    public int Width { get; }

    public int Height { get; }

    public Graphics Graphics { get; }

    /// Показать левый верхний кусок холста размером size в окне hwnd, левым верхним углом в origin.
    public void Present(nint hwnd, Point origin, Size size)
    {
        Graphics.Flush(System.Drawing.Drawing2D.FlushIntention.Sync);
        var destination = new User32.POINT(origin.X, origin.Y);
        var part = new User32.SIZE(Math.Min(size.Width, Width), Math.Min(size.Height, Height));
        var source = new User32.POINT(0, 0);
        var blend = new User32.BLENDFUNCTION
        {
            BlendOp = User32.AC_SRC_OVER,
            SourceConstantAlpha = 255,
            AlphaFormat = User32.AC_SRC_ALPHA,
        };
        User32.UpdateLayeredWindow(hwnd, 0, ref destination, ref part, _memoryDc, ref source, 0, ref blend, User32.ULW_ALPHA);
    }

    public void Dispose()
    {
        Graphics.Dispose();
        _bitmap.Dispose();
        Gdi32.SelectObject(_memoryDc, _previousBitmap);
        Gdi32.DeleteObject(_dib);
        Gdi32.DeleteDC(_memoryDc);
    }
}
