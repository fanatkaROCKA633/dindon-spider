namespace SpiderWidget;

/// Что помнить между запусками: пока только выбранную голову. Файл лежит рядом с логом,
/// только на этом компьютере. Не прочитали или не записали — работаем с головой по умолчанию.
internal static class Settings
{
    private const string HeadKey = "head=";

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SpiderWidget", "settings.txt");

    public static string? LoadHeadId()
    {
        try
        {
            string? line = File.ReadLines(FilePath).FirstOrDefault(l => l.StartsWith(HeadKey, StringComparison.Ordinal));
            return line?[HeadKey.Length..].Trim();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static void SaveHeadId(string id)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, HeadKey + id + Environment.NewLine);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Не запомнили — в следующий раз будет голова по умолчанию; паук работает дальше.
        }
    }
}
