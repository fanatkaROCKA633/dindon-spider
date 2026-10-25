using System.Runtime.InteropServices;
using SpiderWidget.Native;

namespace SpiderWidget.Targets;

/// Видимые окна, панели задач и иконки рабочего стола как опоры. Только чтение
/// положения окон: в чужие процессы не лезем, их память не читаем.
internal static class WindowSurfaces
{
    private const int MinWidth = 100;
    private const int MinHeight = 50;

    private static readonly HashSet<string> TaskbarClasses = ["Shell_TrayWnd", "Shell_SecondaryTrayWnd"];

    // Рабочий стол — фон под всеми окнами, опорой не считается.
    private static readonly HashSet<string> DesktopClasses = ["Progman", "WorkerW"];

    public static List<Surface> Collect()
    {
        var windows = new List<WindowInfo>();
        uint ownProcess = (uint)Environment.ProcessId;
        foreach (nint handle in EnumerateTopToBottom())
        {
            if (Describe(handle, ownProcess) is WindowInfo info)
            {
                windows.Add(info);
            }
        }

        // Иконки лежат на рабочем столе, под всеми окнами, — в конец Z-порядка.
        // Handle 0: иконку не «несём» вместе с пауком, если её перетащили, — он просто упадёт.
        windows.AddRange(DesktopIcons.Collect().Select(r => new WindowInfo(0, r, SurfaceKind.Icon)));
        return Surface.TopEdges(windows);
    }

    private static WindowInfo? Describe(nint handle, uint ownProcess)
    {
        if (!User32.IsWindowVisible(handle) || User32.IsIconic(handle) || IsCloaked(handle))
        {
            return null;
        }

        User32.GetWindowThreadProcessId(handle, out uint process);
        if (process == ownProcess)
        {
            return null;
        }

        string className = ClassName(handle);
        if (DesktopClasses.Contains(className))
        {
            return null;
        }

        bool taskbar = TaskbarClasses.Contains(className);
        long exStyle = User32.GetWindowLongPtr(handle, User32.GWL_EXSTYLE);
        if (!taskbar && (exStyle & User32.WS_EX_TOOLWINDOW) != 0)
        {
            return null; // подсказки, всплывашки, оверлеи
        }

        if (FrameBounds(handle) is not Rectangle bounds || bounds.Width < MinWidth || bounds.Height < MinHeight)
        {
            return null;
        }

        return new WindowInfo(handle, bounds, taskbar ? SurfaceKind.Taskbar : SurfaceKind.Window);
    }

    private static unsafe bool IsCloaked(nint handle)
    {
        int cloaked = 0;
        int hr = Dwmapi.DwmGetWindowAttribute(handle, Dwmapi.DWMWA_CLOAKED, &cloaked, sizeof(int));
        return hr == 0 && cloaked != 0;
    }

    // Рамка без невидимых полей для ресайза: так кромка совпадает с видимым верхом окна.
    private static unsafe Rectangle? FrameBounds(nint handle)
    {
        Dwmapi.RECT rect;
        if (Dwmapi.DwmGetWindowAttribute(handle, Dwmapi.DWMWA_EXTENDED_FRAME_BOUNDS, &rect, sizeof(Dwmapi.RECT)) != 0)
        {
            return null;
        }

        return Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
    }

    private static unsafe string ClassName(nint handle)
    {
        char* buffer = stackalloc char[256];
        int length = User32.GetClassName(handle, buffer, 256);
        return new string(buffer, 0, length);
    }

    // EnumWindows отдаёт окна верхнего уровня в Z-порядке сверху вниз.
    private static unsafe List<nint> EnumerateTopToBottom()
    {
        var handles = new List<nint>();
        GCHandle list = GCHandle.Alloc(handles);
        try
        {
            User32.EnumWindows(&AddHandle, GCHandle.ToIntPtr(list));
        }
        finally
        {
            list.Free();
        }

        return handles;
    }

    [UnmanagedCallersOnly]
    private static int AddHandle(nint handle, nint list)
    {
        ((List<nint>)GCHandle.FromIntPtr(list).Target!).Add(handle);
        return 1;
    }
}
