using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace TrayMeters;

internal sealed class MetricTrayIcon : IDisposable
{
    private readonly string _label;
    private readonly Color _color;
    private readonly Func<int> _sample;
    private readonly NotifyIcon _notifyIcon;
    private IntPtr _currentHIcon = IntPtr.Zero;

    public MetricTrayIcon(string label, Color color, Func<int> sample, ContextMenuStrip menu)
    {
        _label = label;
        _color = color;
        _sample = sample;
        _notifyIcon = new NotifyIcon
        {
            ContextMenuStrip = menu,
            Text = $"{label}: --%",
        };
    }

    public void Update()
    {
        int pct = Math.Clamp(_sample(), 0, 100);

        // Render at native 16x16 tray icon size directly so Windows never blurs the digits via
        // downscaling. Digits are hand-crafted 5x11 bitmaps that fill 11 of the 16 available pixels
        // in height — as large as physically possible on a 16x16 notification icon while still
        // fitting a full black outline for maximum contrast on any taskbar color.
        using var bmp = new Bitmap(16, 16);
        DigitRenderer.Render(bmp, pct, _color);

        IntPtr newHIcon = bmp.GetHicon();
        var newIcon = Icon.FromHandle(newHIcon);

        var oldIcon = _notifyIcon.Icon;
        _notifyIcon.Icon = newIcon;
        _notifyIcon.Text = Truncate($"{_label}: {pct}%", 63);

        oldIcon?.Dispose();
        if (_currentHIcon != IntPtr.Zero)
        {
            NativeMethods.DestroyIcon(_currentHIcon);
        }
        _currentHIcon = newHIcon;

        _notifyIcon.Visible = true;
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s.Substring(0, max);

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Icon?.Dispose();
        if (_currentHIcon != IntPtr.Zero)
        {
            NativeMethods.DestroyIcon(_currentHIcon);
            _currentHIcon = IntPtr.Zero;
        }
        _notifyIcon.Dispose();
    }
}
