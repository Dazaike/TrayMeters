// DigitRenderer.cs — draws pixel-perfect maximum-size numbers onto a 16x16 icon bitmap.
// Fits the entire 16x16 tray icon area with crisp, razor-sharp readable digits.
// No GDI+ text rendering blur, no subpixel scaling artifacts.
namespace TrayMeters;

internal static class DigitRenderer
{
    // 5x11 glyphs for digits 0-9 (11 rows, 5 bits each, MSB left)
    private static readonly ushort[][] Glyphs5x11 = new ushort[10][]
    {
        new ushort[] { 0x0E, 0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0E }, // 0
        new ushort[] { 0x04, 0x0C, 0x14, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x1F }, // 1
        new ushort[] { 0x0E, 0x11, 0x11, 0x01, 0x02, 0x04, 0x08, 0x10, 0x10, 0x10, 0x1F }, // 2
        new ushort[] { 0x1E, 0x01, 0x01, 0x01, 0x0E, 0x01, 0x01, 0x01, 0x01, 0x01, 0x1E }, // 3
        new ushort[] { 0x02, 0x06, 0x0A, 0x12, 0x12, 0x12, 0x1F, 0x02, 0x02, 0x02, 0x02 }, // 4
        new ushort[] { 0x1F, 0x10, 0x10, 0x10, 0x1E, 0x01, 0x01, 0x01, 0x01, 0x11, 0x0E }, // 5
        new ushort[] { 0x0E, 0x11, 0x10, 0x10, 0x1E, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0E }, // 6
        new ushort[] { 0x1F, 0x01, 0x02, 0x02, 0x04, 0x04, 0x08, 0x08, 0x08, 0x08, 0x08 }, // 7
        new ushort[] { 0x0E, 0x11, 0x11, 0x11, 0x0E, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0E }, // 8
        new ushort[] { 0x0E, 0x11, 0x11, 0x11, 0x11, 0x0F, 0x01, 0x01, 0x01, 0x11, 0x0E }, // 9
    };

    // Compact 3x11 '1' and 4x11 '0' specifically to fit "100" into 16x16 with outline!
    private static readonly ushort[] Glyph1_3x11 = new ushort[11] { 0x2, 0x6, 0x2, 0x2, 0x2, 0x2, 0x2, 0x2, 0x2, 0x2, 0x7 };
    private static readonly ushort[] Glyph0_4x11 = new ushort[11] { 0x6, 0x9, 0x9, 0x9, 0x9, 0x9, 0x9, 0x9, 0x9, 0x9, 0x6 };

    public static void Render(Bitmap bmp, int pct, Color metricColor)
    {
        // Digits sit vertically centered at oy = 2 (rows 2-12 of 0-15),
        // leaving 2px headroom and 3px footroom with clean 1px black outline.
        const int oy = 2;

        if (pct >= 100)
        {
            // "100": 3 + 1 + 4 + 1 + 4 = 13px wide, 11px tall. Centered horizontally: ox = 1.
            const int ox = 1;
            Span<(int dx, int dy)> offsets = stackalloc (int, int)[] { (-1,0),(1,0),(0,-1),(0,1) };

            foreach (var (dx, dy) in offsets)
            {
                DrawCustom(bmp, Glyph1_3x11, 3, ox + dx, oy + dy, Color.Black);
                DrawCustom(bmp, Glyph0_4x11, 4, ox + 4 + dx, oy + dy, Color.Black);
                DrawCustom(bmp, Glyph0_4x11, 4, ox + 9 + dx, oy + dy, Color.Black);
            }
            DrawCustom(bmp, Glyph1_3x11, 3, ox, oy, metricColor);
            DrawCustom(bmp, Glyph0_4x11, 4, ox + 4, oy, metricColor);
            DrawCustom(bmp, Glyph0_4x11, 4, ox + 9, oy, metricColor);
        }
        else if (pct >= 10)
        {
            // 2 digits: 5 + 1 + 5 = 11px wide, 11px tall. Centered horizontally: ox = 2.
            int d1 = pct / 10;
            int d2 = pct % 10;
            const int ox = 2;
            Span<(int dx, int dy)> offsets = stackalloc (int, int)[] { (-1,0),(1,0),(0,-1),(0,1) };

            foreach (var (dx, dy) in offsets)
            {
                DrawGlyph(bmp, d1, ox + dx, oy + dy, Color.Black);
                DrawGlyph(bmp, d2, ox + 6 + dx, oy + dy, Color.Black);
            }
            DrawGlyph(bmp, d1, ox, oy, metricColor);
            DrawGlyph(bmp, d2, ox + 6, oy, metricColor);
        }
        else
        {
            // 1 digit: 5px wide, 11px tall. Centered horizontally: ox = 5.
            int d = Math.Max(0, pct);
            const int ox = 5;
            Span<(int dx, int dy)> offsets = stackalloc (int, int)[] { (-1,0),(1,0),(0,-1),(0,1) };

            foreach (var (dx, dy) in offsets)
            {
                DrawGlyph(bmp, d, ox + dx, oy + dy, Color.Black);
            }
            DrawGlyph(bmp, d, ox, oy, metricColor);
        }
    }

    private static void DrawGlyph(Bitmap bmp, int digit, int ox, int oy, Color col)
    {
        ushort[] rows = Glyphs5x11[digit];
        for (int r = 0; r < 11; r++)
        {
            ushort row = rows[r];
            for (int c = 0; c < 5; c++)
            {
                if ((row & (1 << (4 - c))) != 0)
                {
                    int px = ox + c;
                    int py = oy + r;
                    if (px >= 0 && px < 16 && py >= 0 && py < 16)
                    {
                        bmp.SetPixel(px, py, col);
                    }
                }
            }
        }
    }

    private static void DrawCustom(Bitmap bmp, ushort[] rows, int width, int ox, int oy, Color col)
    {
        for (int r = 0; r < 11; r++)
        {
            ushort row = rows[r];
            for (int c = 0; c < width; c++)
            {
                if ((row & (1 << (width - 1 - c))) != 0)
                {
                    int px = ox + c;
                    int py = oy + r;
                    if (px >= 0 && px < 16 && py >= 0 && py < 16)
                    {
                        bmp.SetPixel(px, py, col);
                    }
                }
            }
        }
    }
}
