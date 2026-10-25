using System.Runtime.InteropServices;

namespace SpiderWidget.Native;

internal static partial class Dwmapi
{
    public const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
    public const int DWMWA_CLOAKED = 14;

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [LibraryImport("dwmapi.dll")]
    public static unsafe partial int DwmGetWindowAttribute(nint hwnd, int attribute, void* value, int size);
}
