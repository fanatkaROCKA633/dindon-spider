using System.Runtime.InteropServices;

namespace SpiderWidget.Native;

// COM-интерфейсы оболочки, нужные только чтобы прочитать позиции иконок рабочего стола.
// В ComImport важен лишь порядок методов в vtable: неиспользуемые объявлены заглушками
// без параметров и никогда не вызываются.
internal static partial class Shell32
{
    public static readonly Guid CLSID_ShellWindows = new("9BA05972-F6A8-11CF-A442-00A0C90A8F39");
    public static readonly Guid SID_STopLevelBrowser = new("4C96BE40-915C-11CF-99D3-00AA004AE837");
    public static readonly Guid IID_IShellBrowser = new("000214E2-0000-0000-C000-000000000046");

    public const int SWC_DESKTOP = 8;
    public const int SWFO_NEEDDISPATCH = 1;
    public const uint SVGIO_ALLVIEW = 2;

    public const uint FO_DELETE = 3;
    public const ushort FOF_SILENT = 0x0004;
    public const ushort FOF_NOCONFIRMATION = 0x0010;
    public const ushort FOF_ALLOWUNDO = 0x0040;        // в корзину, а не насовсем
    public const ushort FOF_WANTNUKEWARNING = 0x4000;  // если в корзину нельзя — спросить, а не удалить молча

    [StructLayout(LayoutKind.Sequential)]
    public struct SHFILEOPSTRUCTW
    {
        public nint Hwnd;
        public uint Func;
        public nint From; // пути через '\0', в конце ещё один '\0'
        public nint To;
        public ushort Flags;
        public int AnyOperationsAborted;
        public nint NameMappings;
        public nint ProgressTitle;
    }

    [LibraryImport("shell32.dll", EntryPoint = "SHFileOperationW")]
    public static partial int SHFileOperation(ref SHFILEOPSTRUCTW operation);
}

[ComImport]
[Guid("85CB6900-4D95-11CF-960C-0080C7F4EE85")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
internal interface IShellWindows
{
    void Count();

    void Item();

    void NewEnum();

    void Register();

    void RegisterPending();

    void Revoke();

    void OnNavigate();

    void OnActivated();

    [return: MarshalAs(UnmanagedType.IDispatch)]
    object FindWindowSW(
        [In, MarshalAs(UnmanagedType.Struct)] ref object location,
        [In, MarshalAs(UnmanagedType.Struct)] ref object locationRoot,
        int windowClass,
        out int hwnd,
        int options);
}

[ComImport]
[Guid("6D5140C1-7436-11CE-8034-00AA006009FA")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IComServiceProvider
{
    [return: MarshalAs(UnmanagedType.IUnknown)]
    object QueryService(ref Guid service, ref Guid riid);
}

[ComImport]
[Guid("000214E2-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellBrowser
{
    void GetWindow();

    void ContextSensitiveHelp();

    void InsertMenusSB();

    void SetMenuSB();

    void RemoveMenusSB();

    void SetStatusTextSB();

    void EnableModelessSB();

    void TranslateAcceleratorSB();

    void BrowseObject();

    void GetViewStateStream();

    void GetControlWindow();

    void SendControlMsg();

    IShellView QueryActiveShellView();
}

[ComImport]
[Guid("000214E3-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellView
{
    nint GetWindow();
}

// IFolderView2 целиком (он продолжает vtable IFolderView): нужен ещё размер иконок.
[ComImport]
[Guid("1AF3A467-214F-4298-908E-06B03E0B39F9")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IFolderView2
{
    void GetCurrentViewMode();

    void SetCurrentViewMode();

    void GetFolder();

    nint Item(int index);

    int ItemCount(uint flags);

    void Items();

    void GetSelectionMarkedItem();

    void GetFocusedItem();

    User32.POINT GetItemPosition(nint pidl);

    User32.POINT GetSpacing();

    void GetDefaultSpacing();

    void GetAutoArrange();

    void SelectItem();

    void SelectAndPositionItems();

    void SetGroupBy();

    void GetGroupBy();

    void SetViewProperty();

    void GetViewProperty();

    void SetTileViewProperties();

    void SetExtendedTileViewProperties();

    void SetText();

    void SetCurrentFolderFlags();

    void GetCurrentFolderFlags();

    void GetSortColumnCount();

    void SetSortColumns();

    void GetSortColumns();

    void GetItem();

    void GetVisibleItem();

    void GetSelectedItem();

    void GetSelection();

    void GetSelectionState();

    void InvokeVerbOnSelection();

    void SetViewModeAndIconSize();

    void GetViewModeAndIconSize(out uint viewMode, out int imageSize);
}
