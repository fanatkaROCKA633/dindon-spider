using System.Runtime.InteropServices;
using SpiderWidget.Native;

namespace SpiderWidget.Targets;

/// Прямоугольники иконок рабочего стола в экранных координатах. Позиции спрашиваем у
/// самого рабочего стола через его COM-интерфейс (IFolderView) — память Explorer не читаем.
internal static class DesktopIcons
{
    private const float IconTopMargin = 6; // логические пиксели от верха ячейки до картинки; подобрано по скриншоту

    // Иконки двигаются редко, а каждый опрос — десятки межпроцессных COM-вызовов в Explorer.
    private const long RefreshMilliseconds = 3000;

    private static List<Rectangle> _cached = [];
    private static long _cachedAt = long.MinValue;

    public static List<Rectangle> Collect()
    {
        long now = Environment.TickCount64;
        if (now - _cachedAt < RefreshMilliseconds)
        {
            return _cached;
        }

        _cachedAt = now;
        try
        {
            _cached = Query();
        }
        catch (Exception e) when (e is COMException or InvalidCastException)
        {
            _cached = []; // Explorer перезапускается или рабочего стола нет — живём без иконок
        }

        return _cached;
    }

    private static List<Rectangle> Query()
    {
        var icons = new List<Rectangle>();
        var released = new List<object>();
        try
        {
            Type type = Type.GetTypeFromCLSID(Shell32.CLSID_ShellWindows, throwOnError: true)!;
            var windows = (IShellWindows)Activator.CreateInstance(type)!;
            released.Add(windows);

            object location = 0; // CSIDL_DESKTOP
            object root = null!; // пустой VARIANT
            object dispatch = windows.FindWindowSW(ref location, ref root, Shell32.SWC_DESKTOP, out _, Shell32.SWFO_NEEDDISPATCH);
            released.Add(dispatch);

            Guid service = Shell32.SID_STopLevelBrowser;
            Guid iid = Shell32.IID_IShellBrowser;
            var browser = (IShellBrowser)((IComServiceProvider)dispatch).QueryService(ref service, ref iid);
            released.Add(browser);

            IShellView view = browser.QueryActiveShellView();
            released.Add(view);
            nint viewWindow = view.GetWindow();

            // Иконки скрыты («Показывать значки рабочего стола» выключено) — опор нет.
            nint list = User32.FindWindowEx(viewWindow, 0, "SysListView32", null);
            if (list == 0 || !User32.IsWindowVisible(list))
            {
                return icons;
            }

            var folder = (IFolderView2)view;

            // Опора — верх самой картинки иконки. По скриншоту: X позиции элемента уже совпадает
            // с левым краем картинки, а Y — верх ячейки, картинка чуть ниже. Размер иконки
            // отдаётся в логических пикселях, переводим в физические по DPI рабочего стола.
            folder.GetViewModeAndIconSize(out _, out int iconSize);
            float scale = User32.GetDpiForWindow(viewWindow) / 96f;
            int icon = (int)MathF.Round(iconSize * scale);
            int top = (int)MathF.Round(IconTopMargin * scale);

            int count = folder.ItemCount(Shell32.SVGIO_ALLVIEW);
            for (int i = 0; i < count; i++)
            {
                nint pidl = folder.Item(i);
                try
                {
                    User32.POINT position = folder.GetItemPosition(pidl);
                    User32.ClientToScreen(viewWindow, ref position);
                    icons.Add(new Rectangle(position.X, position.Y + top, icon, icon));
                }
                finally
                {
                    Marshal.FreeCoTaskMem(pidl);
                }
            }

            return icons;
        }
        finally
        {
            // Разные интерфейсы могут оказаться одним и тем же RCW — освобождаем каждый один раз.
            foreach (object com in released.Distinct(ReferenceEqualityComparer.Instance))
            {
                Marshal.FinalReleaseComObject(com);
            }
        }
    }
}
