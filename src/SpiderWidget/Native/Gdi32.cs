using System.Runtime.InteropServices;

namespace SpiderWidget.Native;

internal static partial class Gdi32
{
    [StructLayout(LayoutKind.Sequential)]
    public struct BITMAPINFOHEADER
    {
        public int Size;
        public int Width;
        public int Height;
        public short Planes;
        public short BitCount;
        public int Compression;
        public int SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public int ClrUsed;
        public int ClrImportant;
    }

    // Для 32-битного BI_RGB таблица цветов не нужна, поэтому передаём только заголовок.
    [LibraryImport("gdi32.dll", SetLastError = true)]
    public static partial nint CreateDIBSection(
        nint hdc, ref BITMAPINFOHEADER header, uint usage, out nint bits, nint section, uint offset);

    [LibraryImport("gdi32.dll", SetLastError = true)]
    public static partial nint CreateCompatibleDC(nint hdc);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DeleteDC(nint hdc);

    [LibraryImport("gdi32.dll")]
    public static partial nint SelectObject(nint hdc, nint h);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DeleteObject(nint ho);
}
