using SpiderWidget.Native;

namespace SpiderWidget.Overlay;

/// Пиксельный спрайт: строки символов и палитра. '.' — прозрачно.
internal sealed class Sprite
{
    private readonly Dictionary<char, SolidBrush> _brushes;

    public Sprite(string[] rows, IReadOnlyDictionary<char, Color> palette)
    {
        Rows = rows;
        Palette = palette;
        // Кисти живут всё время работы: рисуем 30 раз в секунду, создавать их каждый раз незачем.
        _brushes = palette.ToDictionary(p => p.Key, p => new SolidBrush(p.Value));
    }

    public string[] Rows { get; }

    public IReadOnlyDictionary<char, Color> Palette { get; }

    public int Width => Rows[0].Length;

    public int Height => Rows.Length;

    public SolidBrush? BrushAt(int x, int y) => _brushes.GetValueOrDefault(Rows[y][x]);

    private Bitmap? _rendered;
    private int _renderedScale;

    /// Спрайт, уже увеличенный в scale раз. Рисуется один раз: копировать готовую картинку
    /// 30 раз в секунду намного дешевле, чем каждый кадр закрашивать сотни квадратиков.
    public Bitmap Rendered(int scale)
    {
        if (_rendered is null || _renderedScale != scale)
        {
            _rendered?.Dispose();
            _rendered = new Bitmap(Width * scale, Height * scale, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using Graphics g = Graphics.FromImage(_rendered);
            Sprites.Draw(g, this, 0, 0, scale);
            _renderedScale = scale;
        }

        return _rendered;
    }
}

/// Голова на выбор. Id — для файла настроек (не меняется), Name — в меню.
/// Open — та же голова с открытым ртом: ждёт еду и жуёт.
internal sealed record HeadStyle(string Id, string Name, Sprite Closed, Sprite Open);

internal static class Sprites
{
    public static readonly Color Ink = Color.FromArgb(255, 83, 83, 83); // цвет ног
    private static readonly SolidBrush InkBrush = new(Ink);

    /// Нити паутины: светло-серые, чуть прозрачные — видны и на светлом, и на тёмном фоне.
    public static readonly SolidBrush WebBrush = new(Color.FromArgb(220, 190, 195, 205));

    /// «Лёвчик» — мультяшный портрет по фото, 26×32, в стиле «Диндона»: тёмные волосы с ровной
    /// чёлкой «под горшок», серо-голубые очки, голубые глаза, большие тёмные усы, колечко в носу,
    /// щетина на подбородке.
    public static readonly Sprite Lyovchik = new(
        [
            ".........kkkkkkkk.........",
            ".......kkhhhhhhhhkk.......",
            ".....kkhhhhlhhhhhhhkk.....",
            "....khhhhhlhhhhlhhhhhk....",
            "...khhhhhlhhhhhhlhhhhhk...",
            "..khhhhHhhhhHhhhhHhhhhhk..",
            "..khhhhHhhhhHhhhhHhhhhhk..",
            ".khhhhhHhhhhHhhhhHhhhhhhk.",
            ".khhhhhHhhhhHhhhhHhhhhhhk.",
            ".khhhhhHhhhhHhhhhHhhhhhhk.",
            "khhhhhhHhhhhHhhhhHhhhhhhhk",
            ".khhhhhHhhhhHhhhhHhhhhhhk.",
            ".khhhshsshssHsshsshsShhhk.",
            ".khhhsssssssssssssssshhhk.",
            "..khhssHHHHssssHHHHsshhk..",
            "..khhgggggggssggggggghhk..",
            "..kggssssssggggssssssggk..",
            "..ksgsswbwsgssgswbwssgSk..",
            "..ksgsswbwsgssgswbwssgSk..",
            "..ksgssssssgssgssssssgSk..",
            "..kSsggggggssssggggggSSk..",
            "...ksssssssssSsssssssSk...",
            "...kSssssssssSsssssssSk...",
            "....ksssssssSsSossssSk....",
            "....ksssssmmmmmmmsssSk....",
            ".....kssmmmmmmmmmmmSk.....",
            ".....kSmmmsssssssmmmk.....",
            "......kSsssrrrrrsSSk......",
            ".......kSSssssssSSk.......",
            "........kkuSuSuSuk........",
            "..........kukukuk.........",
            "...........k.k.k..........",
        ],
        new Dictionary<char, Color>
        {
            ['k'] = Color.FromArgb(24, 24, 24),    // контур
            ['g'] = Color.FromArgb(92, 102, 124),  // оправа очков
            ['w'] = Color.FromArgb(245, 247, 250), // белки
            ['b'] = Color.FromArgb(70, 112, 172),  // голубые глаза
            ['s'] = Color.FromArgb(246, 210, 192), // кожа
            ['S'] = Color.FromArgb(224, 176, 156), // тень кожи
            ['h'] = Color.FromArgb(62, 44, 36),    // волосы
            ['H'] = Color.FromArgb(40, 28, 24),    // пряди чёлки, брови
            ['l'] = Color.FromArgb(98, 72, 58),    // блик на макушке
            ['m'] = Color.FromArgb(58, 38, 30),    // усы
            ['r'] = Color.FromArgb(196, 124, 122), // губы
            ['u'] = Color.FromArgb(204, 164, 146), // щетина
            ['o'] = Color.FromArgb(205, 210, 220), // колечко в носу
        });

    public static readonly Sprite LyovchikOpen = WithArea(Lyovchik, [(27, 11, 15, 'H'), (28, 11, 15, 'r')]);

    /// «Илюха» — пиксельный портрет с фото: 24×38, 10 цветов + тёмный 'k' (контур, оправа очков,
    /// зрачки), чтобы читалась и на светлом, и на тёмном фоне. Та же голова, что в ilyuha_plg.
    public static readonly Sprite Ilyuha = new(
        [
            "..........k.kk..........",
            ".........kkkefkk........",
            ".......kkhdfhiijkk......",
            ".....kkiiihdhfdiiik.....",
            "....kigfgigcffhhhihk....",
            "....khffiiddgehihhik....",
            "...kfeghifchfeegihhhk...",
            "...kdddefgeghgedeffek...",
            "..kffecddefffhhhhdkkkk..",
            "..khgffecbcccceeebkkkk..",
            ".khdegeecbkkckcccckkkkk.",
            ".kgdccbccccbcdeeaackkkk.",
            ".keggddddcbcaeffefdkkck.",
            ".kehighgeaaeffggfffeeck.",
            ".kdiiiigfffffgggfghhgck.",
            ".keijgcbcefgggeckkehgck.",
            ".kfihccddefhgfdcbkkffck.",
            "kkkkcccckcfgfkkkkkkkkkk.",
            "kkkigggdckkkkkbcedddckkk",
            "kkkieeckbckkkbckkccdgkk.",
            ".kkigfckdbkgkbckkccggkk.",
            ".kkjjheegkhjdkeccdehkkk.",
            ".kkejjiihkjjgkfffghikk..",
            ".kikkkkkkfjjickkkkkkk...",
            ".kjjiiihgijjjfccdefgik..",
            ".kiiijighjjjjifgghhhik..",
            "..kjiihegiihhhffhhhhk...",
            "..kjjiggheddddggghihk...",
            "..kijihhhhfddfgggiihk...",
            "..kjiigedfffeeddghhhk...",
            "...khhfeeaaaaaeefggjk...",
            "...kegefggeaafgffgehk...",
            "....kdffgggfffffffkk....",
            "....kkdgiiihgggggckk....",
            ".....kkchiihghheekk.....",
            "......kkkeggfgedkk......",
            "........kkkkccck........",
            ".........kkkkkk.........",
        ],
        new Dictionary<char, Color>
        {
            ['a'] = Color.FromArgb(26, 20, 20),
            ['b'] = Color.FromArgb(62, 44, 39),
            ['c'] = Color.FromArgb(88, 64, 56),
            ['d'] = Color.FromArgb(112, 80, 69),
            ['e'] = Color.FromArgb(133, 95, 82),
            ['f'] = Color.FromArgb(151, 108, 93),
            ['g'] = Color.FromArgb(168, 120, 103),
            ['h'] = Color.FromArgb(180, 136, 119),
            ['i'] = Color.FromArgb(204, 157, 138),
            ['j'] = Color.FromArgb(239, 187, 166),
            ['k'] = Color.FromArgb(24, 24, 24),
        });

    public static readonly Sprite IlyuhaOpen = WithArea(Ilyuha, [(30, 8, 14, 'a'), (31, 8, 14, 'a'), (32, 9, 13, 'a')]);

    /// «Диндон» — мультяшный портрет по фото, 26×34: длинные прямые волосы с пробором и светлой
    /// прядью, узкие чёрные солнечные очки, тёмно-красные губы, серёжка.
    public static readonly Sprite Dindon = new(
        [
            "..........kkkkkk..........",
            ".......kkkhhhhhhkkk.......",
            "......khhhhhhHhhhhhk......",
            ".....khhhhhlhHhlhhhhk.....",
            "....khhhhhlhhHhhlhhhhk....",
            "...khhhhhlhhhHhhhlhhhhk...",
            "..kHhhhhhhhhhHhhhhhhhhHk..",
            "..kHhhhhhhhhhHhhhhhhhhHk..",
            ".khHhhhhhhhhhHhhhhhhhhHhk.",
            ".khHhhhhhhhhhhhhhhhhhhHhk.",
            "khhHhhhhhhhhsshhhhhhhhHhhk",
            "khhHhhhhhhsssssshhhhhhHhhk",
            "khhHyhhsssssssssssshhhHhhk",
            "khhHyhssHHHHsssHHHHShhHhhk",
            "khhHyhsssssssssssssShhHhhk",
            "khhHyhsgggggsssgggggshHhhk",
            "khhHyhgwqgggggggwqggghHhhk",
            "khhHySggggggsssgggggghHhhk",
            "khhHyyggggggsssgggggghHhhk",
            "khhHyysssssssssssssShhHhhk",
            "khhHyysssssssSsssssShhHhhk",
            "khhHyossppssSSsssppShhHhhk",
            "khhHyoHsssssssssssSHhhHhhk",
            "khhHyyHSssssrrrsssSHhhHhhk",
            "khhHyyHhSssrRRrrsShHhhHhhk",
            "khhHyyHhhSssrrrSShhHhhHhhk",
            "khhHyyHhhhhSSSSShhhHhhHhhk",
            "khhHyyHhhhhSSSSShhhHhhHhhk",
            "khhHyyHhhhhSSSSShhhHhhHhhk",
            "khhHyyHhhhhSSSSShhhHhhHhhk",
            "khhHyyHhhhhhhhhhhhhHhhHhhk",
            "khhkhhkhhkhhkhhkhhkHhkHhk.",
            ".khkkhkkhkkhkkhkkhkkhkkhk.",
            "..k..k..k..k..k..k..k..k..",
        ],
        new Dictionary<char, Color>
        {
            ['k'] = Color.FromArgb(24, 24, 24),    // контур
            ['g'] = Color.FromArgb(10, 10, 12),    // солнечные очки
            ['w'] = Color.FromArgb(235, 240, 245), // блик на очках
            ['q'] = Color.FromArgb(90, 95, 105),
            ['s'] = Color.FromArgb(250, 218, 200), // кожа
            ['S'] = Color.FromArgb(230, 184, 162), // тень кожи, шея
            ['p'] = Color.FromArgb(242, 168, 160), // румянец
            ['h'] = Color.FromArgb(176, 124, 78),  // волосы
            ['H'] = Color.FromArgb(128, 86, 52),   // пряди, пробор, брови
            ['l'] = Color.FromArgb(212, 166, 112), // блик на макушке
            ['y'] = Color.FromArgb(234, 208, 160), // светлая прядь
            ['r'] = Color.FromArgb(140, 30, 42),   // губы
            ['R'] = Color.FromArgb(182, 58, 70),   // блик на губах
            ['o'] = Color.FromArgb(222, 182, 84),  // серёжка
        });

    public static readonly Sprite DindonOpen = WithArea(Dindon, [(24, 11, 15, 'g'), (25, 12, 14, 'g'), (26, 12, 14, 'r')]);

    /// Все головы — в меню трея в этом порядке; первая — по умолчанию.
    public static readonly IReadOnlyList<HeadStyle> Heads =
    [
        new("dindon", "Диндон", Dindon, DindonOpen),
        new("lyovchik", "Лёвчик", Lyovchik, LyovchikOpen),
        new("ilyuha", "Илюха", Ilyuha, IlyuhaOpen),
    ];

    // Копия спрайта, где в строке row от столбца from до to включительно — цвет color.
    private static Sprite WithArea(Sprite sprite, (int Row, int From, int To, char Color)[] areas)
    {
        string[] rows = (string[])sprite.Rows.Clone();
        foreach ((int row, int from, int to, char color) in areas)
        {
            char[] line = rows[row].ToCharArray();
            for (int x = from; x <= to; x++)
            {
                line[x] = color;
            }

            rows[row] = new string(line);
        }

        return new Sprite(rows, sprite.Palette);
    }

    /// Рисует спрайт в точку (left, top) в пикселях холста, каждый логический пиксель — scale×scale.
    public static void Draw(Graphics g, Sprite sprite, int left, int top, int scale)
    {
        for (int y = 0; y < sprite.Height; y++)
        {
            for (int x = 0; x < sprite.Width; x++)
            {
                if (sprite.BrushAt(x, y) is SolidBrush brush)
                {
                    g.FillRectangle(brush, left + x * scale, top + y * scale, scale, scale);
                }
            }
        }
    }

    /// Пиксельная линия (Брезенхэм) между логическими пикселями a и b, каждый — scale×scale.
    public static void DrawLine(Graphics g, Point a, Point b, int scale) => DrawLine(g, a, b, scale, InkBrush);

    public static void DrawLine(Graphics g, Point a, Point b, int scale, Brush brush)
    {
        int dx = Math.Abs(b.X - a.X);
        int dy = -Math.Abs(b.Y - a.Y);
        int stepX = a.X < b.X ? 1 : -1;
        int stepY = a.Y < b.Y ? 1 : -1;
        int error = dx + dy;
        int x = a.X;
        int y = a.Y;
        while (true)
        {
            g.FillRectangle(brush, x * scale, y * scale, scale, scale);
            if (x == b.X && y == b.Y)
            {
                return;
            }

            int doubled = 2 * error;
            if (doubled >= dy)
            {
                error += dy;
                x += stepX;
            }

            if (doubled <= dx)
            {
                error += dx;
                y += stepY;
            }
        }
    }

    /// Иконка трея из головы: квадрат по большей стороне, голова по центру.
    public static Icon CreateTrayIcon(Sprite head)
    {
        int side = Math.Max(head.Width, head.Height);
        using var bitmap = new Bitmap(side, side);
        using (var g = Graphics.FromImage(bitmap))
        {
            Draw(g, head, (side - head.Width) / 2, (side - head.Height) / 2, 1);
        }

        nint handle = bitmap.GetHicon();
        try
        {
            // Icon.FromHandle не владеет дескриптором; клон — владеет, исходный освобождаем.
            using var borrowed = Icon.FromHandle(handle);
            return (Icon)borrowed.Clone();
        }
        finally
        {
            User32.DestroyIcon(handle);
        }
    }
}
