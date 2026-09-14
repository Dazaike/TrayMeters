# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [v1.0.0] - 2026-09-14

### Added
- Three standalone notification area (system tray) meter icons for live system load:
  - **CPU:** `Processor\% Processor Time\_Total` performance counter (deep sky blue).
  - **Memory:** `GlobalMemoryStatusEx` physical load percent (lime green).
  - **GPU:** `GPU Engine\Utilization Percentage` performance counters (orange-red).
- Custom 5×11 pixel bitmap font renderer (`DigitRenderer.cs`) delivering razor-sharp, native 16×16 icons with 1px black outline halo for maximum readability across light and dark taskbars.
- Multi-GPU selector context menu allowing users to pin the GPU tray icon to a specific physical adapter or track the busiest GPU automatically.
- DXGI adapter enumeration (`GpuAdapterNames.cs`) mapping GPU Engine counter LUIDs to friendly hardware device descriptions (e.g. `NVIDIA GeForce RTX 4070`, `Intel(R) UHD Graphics 770`).
- Persistent user settings storage in `%AppData%\TrayMeters\settings.json`.
- GDI handle lifecycle management via Win32 `DestroyIcon` to prevent handle exhaustion over long runtimes.
- Shared context menu on all three icons with GPU selection and Exit.
