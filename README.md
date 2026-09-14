# TrayMeters

Lightweight Windows notification-area (system tray) resource monitor. Displays live **CPU**, **Memory**, and **GPU** usage as three separate, high-contrast tray icons with razor-sharp native 16×16 pixel-rendered numbers and selectable GPU tracking.

![TrayMeters](https://img.shields.io/badge/version-1.0.0-blue.svg)
![Target](https://img.shields.io/badge/.NET-8.0--windows-purple.svg)
![Platform](https://img.shields.io/badge/platform-Windows_10_%2F_11-lightgrey.svg)

---

## Features

- **Live System Tray Badges:**
  - 🔵 **CPU Load (%)** — via Windows `Processor\% Processor Time\_Total` performance counter.
  - 🟢 **Memory Load (%)** — via Win32 `GlobalMemoryStatusEx` (`dwMemoryLoad`, approximate physical memory in use).
  - 🔴 **GPU Utilization (%)** — via `GPU Engine\Utilization Percentage` performance counters, aggregated per GPU adapter and engine type (matching Windows Task Manager's calculation).
- **Pixel-Perfect 16×16 Render:** Custom 5×11 pixel bitmap digits with 1px black outline halo rendered directly at the native Windows tray icon resolution (16×16) to eliminate downscaling blur and subpixel artifacts. Digits fill 70% of the vertical icon tile for maximum readability at any scale.
- **Multi-GPU Selector:** Supports systems with multiple installed graphics cards (e.g., Intel integrated GPU + discrete NVIDIA/AMD GPU). Right-click any icon → **GPU Source**:
  - `Busiest GPU (auto)` (default)
  - Dedicated entries for each detected physical adapter (e.g. `NVIDIA GeForce RTX 4070`, `Intel(R) UHD Graphics 770`), enumerated via DXGI COM interop.
  - Remembers your selection across restarts via `%AppData%\TrayMeters\settings.json`.
- **Zero GDI Leaks:** Safely calls Win32 `DestroyIcon` after every redraw tick, avoiding GDI handle leaks that plague naive `NotifyIcon` implementations.
- **Windowless & Lightweight:** Runs completely in the background without taskbar clutter or empty forms.
- **Shared Context Menu:** Right-click any of the three icons to access GPU settings or exit the app.

---

## Requirements

- Windows 10 / 11 (x64)
- [.NET 8.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (`Microsoft.WindowsDesktop.App 8.0.x`)

---

## Installation & Running

### Download Release
1. Download `TrayMeters-v1.0.0-win-x64.zip` from the [latest GitHub Release](../../releases).
2. Extract the archive.
3. Run `TrayMeters.exe`.
4. Three icons will appear in your system tray (check the `^` overflow menu if hidden by Windows).

### Build from Source
```powershell
git clone https://github.com/Dazaike/TrayMeters.git
cd TrayMeters
dotnet build -c Release
dotnet run -c Release
```

---

## Configuration

Preferences are stored in:
```
%AppData%\TrayMeters\settings.json
```

Example:
```json
{
  "SelectedGpuLuid": "0x00000000_0x00016F42"
}
```
Setting `SelectedGpuLuid` to `null` reverts to tracking the busiest GPU automatically.

---

## Changelog

See [CHANGELOG.md](CHANGELOG.md) for full release history.
