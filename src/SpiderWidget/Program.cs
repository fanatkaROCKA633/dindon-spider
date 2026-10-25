using SpiderWidget.Overlay;

namespace SpiderWidget;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        // Второй паук не нужен: если мьютекс уже занят, тихо выходим.
        using var singleInstance = new Mutex(true, $@"Local\{Settings.AppName}.SingleInstance", out bool isFirst);
        if (!isFirst)
        {
            return;
        }

        ApplicationConfiguration.Initialize();

        // Голова — из прошлого запуска; незнакомая или нет настроек — первая из списка.
        string? savedId = Settings.Get(Settings.HeadKey);
        HeadStyle head = Sprites.Heads.FirstOrDefault(h => h.Id == savedId) ?? Sprites.Heads[0];

        using var spider = new SpiderForm(head);
        Dictionary<HeadStyle, Icon> icons = Sprites.Heads.ToDictionary(h => h, h => Sprites.CreateTrayIcon(h.Closed));
        using var menu = new ContextMenuStrip();
        using var tray = new NotifyIcon
        {
            Icon = icons[head],
            Text = Settings.AppName,
            ContextMenuStrip = menu,
            Visible = true,
        };

        var heads = new ToolStripMenuItem("Голова");
        foreach (HeadStyle style in Sprites.Heads)
        {
            var item = new ToolStripMenuItem(style.Name) { Checked = style == head };
            item.Click += (_, _) =>
            {
                spider.SetHead(style);
                tray.Icon = icons[style];
                Settings.Set(Settings.HeadKey, style.Id);
                foreach (ToolStripMenuItem other in heads.DropDownItems)
                {
                    other.Checked = other == item;
                }
            };
            heads.DropDownItems.Add(item);
        }

        // Паутина включена, пока её явно не выключили в меню.
        var webs = new ToolStripMenuItem("Паутина") { CheckOnClick = true, Checked = Settings.Get(Settings.WebsKey) != "off" };
        spider.WebsEnabled = webs.Checked;
        webs.CheckedChanged += (_, _) =>
        {
            spider.WebsEnabled = webs.Checked;
            Settings.Set(Settings.WebsKey, webs.Checked ? "on" : "off");
        };

        var pause = new ToolStripMenuItem("Пауза") { CheckOnClick = true };
        pause.CheckedChanged += (_, _) => spider.Paused = pause.Checked;
        menu.Items.Add(heads);
        menu.Items.Add(webs);
        menu.Items.Add(pause);
        menu.Items.Add("Выход", null, (_, _) => Application.Exit());

        using DebugSurfacesForm? debug = args.Contains("--debug-surfaces") ? new DebugSurfacesForm() : null;
        if (debug != null)
        {
            spider.SurfacesRefreshed += debug.ShowSurfaces;
            debug.Show();
        }

        Application.Run(spider);
        tray.Visible = false;
        foreach (Icon icon in icons.Values)
        {
            icon.Dispose();
        }
    }
}
