using System.Runtime.InteropServices;
using SpiderWidget.Native;

namespace SpiderWidget.Recycle;

/// Итог проверки. Normalized — путь, который проверили и который можно отдавать в корзину.
internal readonly record struct PathCheck(bool Allowed, string? Reason, string? Normalized = null)
{
    public static PathCheck Ok(string normalized) => new(true, null, normalized);

    public static PathCheck Reject(string reason) => new(false, reason);
}

/// Что считать опасным на этой машине. Отдельно от проверки, чтобы её можно было
/// тестировать без настоящих дисков и папок.
/// SystemTrees — нельзя ни саму папку, ни что-либо внутри, ни папку, в которой она лежит.
/// UserFolders — нельзя саму папку и папку, в которой она лежит; файлы внутри — можно.
/// LongPath — раскрывает короткие имена 8.3 (C:\PROGRA~1 → C:\Program Files).
internal sealed partial record PathRules(
    IReadOnlyList<string> SystemTrees,
    IReadOnlyList<string> UserFolders,
    Func<string, DriveType> DriveTypeOf,
    Func<string, string> LongPath)
{
    public static PathRules Current() => new(
        [
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        ],
        [
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
        ],
        root =>
        {
            try
            {
                return new DriveInfo(root).DriveType;
            }
            catch (Exception e) when (e is ArgumentException or IOException)
            {
                return DriveType.Unknown;
            }
        },
        Native.LongPath);

    private static partial class Native
    {
        [LibraryImport("kernel32.dll", EntryPoint = "GetLongPathNameW", StringMarshalling = StringMarshalling.Utf16)]
        private static partial uint GetLongPathName(string shortPath, [Out] char[] longPath, uint size);

        // Для несуществующего пути Windows ничего не раскроет — тогда оставляем как есть.
        public static string LongPath(string path)
        {
            var buffer = new char[32768];
            uint length = GetLongPathName(path, buffer, (uint)buffer.Length);
            return length > 0 && length < buffer.Length ? new string(buffer, 0, (int)length) : path;
        }
    }
}

/// Отправка в корзину того, что бросили на паука. Сначала строгий фильтр путей:
/// любое сомнение — отказ, ничего не удаляем.
internal static class RecycleBin
{
    private const long MaxLogBytes = 1024 * 1024;

    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SpiderWidget", "log.txt");

    /// Проверяет каждый путь и отправляет прошедшие в корзину одним вызовом. Возвращает,
    /// сколько путей ушло в корзину и сколько отклонено. Всё пишется в локальный лог.
    public static (int Recycled, int Rejected) Eat(IReadOnlyList<string> paths, PathRules rules, nint owner)
    {
        var allowed = new List<string>();
        foreach (string path in paths)
        {
            PathCheck check = Check(path, rules);
            if (check.Allowed)
            {
                allowed.Add(check.Normalized!); // в корзину — ровно то, что проверили
            }
            else
            {
                Log($"отказ: {path} — {check.Reason}");
            }
        }

        if (allowed.Count == 0)
        {
            return (0, paths.Count);
        }

        (int code, bool aborted) = SendToRecycleBin(allowed, owner);
        string result = code != 0 ? $"ошибка 0x{code:X}" : aborted ? "отменено пользователем" : "в корзине";
        foreach (string path in allowed)
        {
            Log($"{result}: {path}");
        }

        int recycled = code == 0 && !aborted ? allowed.Count(p => !File.Exists(p) && !Directory.Exists(p)) : 0;
        return (recycled, paths.Count - allowed.Count);
    }

    // Только FO_DELETE с FOF_ALLOWUNDO — это корзина. Подтверждение не спрашиваем (так решили
    // в спеке), но FOF_WANTNUKEWARNING заставляет Windows спросить, если в корзину положить нельзя.
    private static (int Code, bool Aborted) SendToRecycleBin(IReadOnlyList<string> paths, nint owner)
    {
        nint from = Marshal.StringToHGlobalUni(string.Join('\0', paths) + '\0'); // + завершающий '\0' от StringToHGlobalUni
        try
        {
            var operation = new Shell32.SHFILEOPSTRUCTW
            {
                Hwnd = owner,
                Func = Shell32.FO_DELETE,
                From = from,
                Flags = (ushort)(Shell32.FOF_ALLOWUNDO | Shell32.FOF_NOCONFIRMATION | Shell32.FOF_WANTNUKEWARNING | Shell32.FOF_SILENT),
            };
            int code = Shell32.SHFileOperation(ref operation);
            return (code, operation.AnyOperationsAborted != 0);
        }
        finally
        {
            Marshal.FreeHGlobal(from);
        }
    }

    // Лог только на этой машине; больше мегабайта — начинаем заново, прошлый сохраняем как .old.
    private static void Log(string line)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            if (File.Exists(LogPath) && new FileInfo(LogPath).Length > MaxLogBytes)
            {
                File.Move(LogPath, LogPath + ".old", overwrite: true);
            }

            File.AppendAllText(LogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {line}{Environment.NewLine}");
        }
        catch (IOException)
        {
            // Лог — не главное: не смогли записать, паук работает дальше.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    public static PathCheck Check(string path, PathRules rules)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
        {
            return PathCheck.Reject("не абсолютный путь");
        }

        // \\server\share, \\?\..., \\.\... — сеть или служебные пути: у сети корзины нет, удаление было бы безвозвратным.
        if (path.StartsWith(@"\\", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal))
        {
            return PathCheck.Reject("сетевой или служебный путь");
        }

        string normalized;
        try
        {
            normalized = Normalize(path, rules);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return PathCheck.Reject("путь не разобрать");
        }

        string root = Path.GetPathRoot(normalized) ?? "";
        if (rules.DriveTypeOf(root) != DriveType.Fixed)
        {
            return PathCheck.Reject("не локальный несъёмный диск — у него нет корзины");
        }

        if (string.Equals(normalized, Normalize(root, rules), StringComparison.OrdinalIgnoreCase))
        {
            return PathCheck.Reject("корень диска");
        }

        foreach (string tree in rules.SystemTrees.Where(t => t.Length > 0).Select(t => Normalize(t, rules)))
        {
            if (IsSameOrInside(normalized, tree) || IsSameOrInside(tree, normalized))
            {
                return PathCheck.Reject($"системная папка {tree}");
            }
        }

        foreach (string folder in rules.UserFolders.Where(f => f.Length > 0).Select(f => Normalize(f, rules)))
        {
            if (IsSameOrInside(folder, normalized))
            {
                return PathCheck.Reject($"папка пользователя {folder} целиком");
            }
        }

        return PathCheck.Ok(normalized);
    }

    // Абсолютный путь без «..», без хвостового «\» (кроме корня), с длинными именами вместо 8.3.
    private static string Normalize(string path, PathRules rules)
    {
        string full = rules.LongPath(Path.GetFullPath(path));
        string trimmed = full.TrimEnd('\\', '/');
        return trimmed.EndsWith(':') ? trimmed + @"\" : trimmed;
    }

    private static bool IsSameOrInside(string path, string folder) =>
        string.Equals(path, folder, StringComparison.OrdinalIgnoreCase)
        || path.StartsWith(folder.TrimEnd('\\') + @"\", StringComparison.OrdinalIgnoreCase);
}
