namespace SpiderWidget;

/// Что помнить между запусками: выбранную голову и включена ли паутина. Файл — строки
/// «ключ=значение» в папке программы, только на этом компьютере. Не прочитали или не
/// записали — работаем со значениями по умолчанию.
internal static class Settings
{
    /// Своё имя у этой копии паука: отдельные настройки, лог и «только один экземпляр»,
    /// чтобы она не мешала пауку из ilyuha_plg и могла работать рядом с ним.
    public const string AppName = "DindonSpider";

    public const string HeadKey = "head";
    public const string WebsKey = "webs";

    public static readonly string DataFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName);

    private static readonly string FilePath = Path.Combine(DataFolder, "settings.txt");

    public static string? Get(string key) => ReadAll().GetValueOrDefault(key);

    public static void Set(string key, string value)
    {
        Dictionary<string, string> all = ReadAll();
        all[key] = value;
        try
        {
            Directory.CreateDirectory(DataFolder);
            File.WriteAllLines(FilePath, all.Select(p => $"{p.Key}={p.Value}"));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Не запомнили — в следующий раз будет значение по умолчанию; паук работает дальше.
        }
    }

    private static Dictionary<string, string> ReadAll()
    {
        var all = new Dictionary<string, string>();
        try
        {
            foreach (string line in File.ReadLines(FilePath))
            {
                int eq = line.IndexOf('=');
                if (eq > 0)
                {
                    all[line[..eq].Trim()] = line[(eq + 1)..].Trim();
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Нет файла — пустые настройки.
        }

        return all;
    }
}
