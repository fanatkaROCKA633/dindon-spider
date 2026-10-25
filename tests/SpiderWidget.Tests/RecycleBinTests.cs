using SpiderWidget.Recycle;

namespace SpiderWidget.Tests;

public class RecycleBinTests
{
    // Выдуманная машина: C: и D: — локальные диски, E: — флешка, Z: — сетевой диск.
    private static readonly PathRules Rules = new(
        [@"C:\Windows", @"C:\Program Files", @"C:\Program Files (x86)"],
        [@"C:\Users\me", @"C:\Users\me\Desktop", @"C:\Users\Public\Desktop"],
        root => char.ToUpperInvariant(root[0]) switch
        {
            'C' or 'D' => DriveType.Fixed,
            'E' => DriveType.Removable,
            'Z' => DriveType.Network,
            _ => DriveType.NoRootDirectory,
        },
        path => path.Replace(@"\PROGRA~1", @"\Program Files", StringComparison.OrdinalIgnoreCase));

    [Theory]
    [InlineData(@"C:\")]
    [InlineData(@"C:")]
    [InlineData(@"D:\")]
    [InlineData(@"C:\Windows")]
    [InlineData(@"c:\windows\")]
    [InlineData(@"C:\Windows\System32\kernel32.dll")]
    [InlineData(@"C:\Program Files")]
    [InlineData(@"C:\Program Files (x86)\App")]
    [InlineData(@"C:\PROGRA~1")]
    [InlineData(@"C:\Users")]
    [InlineData(@"C:\Users\me")]
    [InlineData(@"C:\Users\me\Desktop")]
    [InlineData(@"C:\Users\me\Desktop\")]
    [InlineData(@"C:\Users\me\Desktop\..")]
    [InlineData(@"C:\Users\me\Desktop\folder\..\..")]
    [InlineData(@"C:\Users\Public\Desktop")]
    [InlineData(@"\\server\share\x.txt")]
    [InlineData(@"\\?\C:\Users\me\Desktop\a.txt")]
    [InlineData(@"\\.\C:\temp\a.txt")]
    [InlineData(@"E:\photos\x.jpg")]
    [InlineData(@"Z:\work\report.docx")]
    [InlineData(@"Q:\x.txt")]
    [InlineData(@"a.txt")]
    [InlineData(@"C:a.txt")]
    [InlineData(@"\Users\me\Desktop\a.txt")]
    [InlineData("")]
    [InlineData("   ")]
    public void DangerousOrUnclearPath_IsRejected(string path)
    {
        PathCheck check = RecycleBin.Check(path, Rules);

        Assert.False(check.Allowed, $"«{path}» пропущен");
        Assert.False(string.IsNullOrEmpty(check.Reason));
    }

    // На настоящих правилах этой машины: папки берутся из Windows, диски — из DriveInfo.
    [Fact]
    public void CurrentRules_RealSystemFoldersRejected_TempFileAllowed()
    {
        PathRules rules = PathRules.Current();

        Assert.False(RecycleBin.Check(Environment.GetFolderPath(Environment.SpecialFolder.Windows), rules).Allowed);
        Assert.False(RecycleBin.Check(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), rules).Allowed);
        Assert.False(RecycleBin.Check(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), rules).Allowed);
        Assert.False(RecycleBin.Check(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), rules).Allowed);
        Assert.False(RecycleBin.Check(Path.GetPathRoot(Environment.SystemDirectory)!, rules).Allowed);

        string temp = Path.Combine(Path.GetTempPath(), "spider-widget-test.txt");
        PathCheck check = RecycleBin.Check(temp, rules);
        Assert.True(check.Allowed, check.Reason);
    }

    [Theory]
    [InlineData(@"C:\Users\me\Desktop\a.txt")]
    [InlineData(@"C:\Users\me\Desktop\Новая папка")]
    [InlineData(@"C:\Users\me\Desktop\Новая папка\")]
    [InlineData(@"C:\Users\me\Desktop\Steam.lnk")]
    [InlineData(@"C:\Users\me\Desktop\folder\..\b.txt")]
    [InlineData(@"C:\Users\me\Downloads\setup.exe")]
    [InlineData(@"C:\Users\Public\Desktop\Game.lnk")]
    [InlineData(@"D:\data\old")]
    [InlineData(@"C:\WindowsOld\file.txt")]
    [InlineData(@"C:\Program Files Backup\x.txt")]
    public void OrdinaryFileOrFolder_IsAllowed(string path)
    {
        PathCheck check = RecycleBin.Check(path, Rules);

        Assert.True(check.Allowed, $"«{path}» отклонён: {check.Reason}");
    }
}
