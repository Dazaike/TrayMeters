using System.Diagnostics;
using System.Runtime.InteropServices;

namespace TrayMeters;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly System.Windows.Forms.Timer _timer;
    private readonly PerformanceCounter _cpuCounter;
    private readonly Dictionary<string, PerformanceCounter> _gpuCounters = new();
    private readonly Settings _settings;
    private readonly List<(string Luid, string Name)> _adapters;
    private string? _selectedLuid;
    private readonly List<ToolStripMenuItem> _gpuMenuItems = new();
    private readonly MetricTrayIcon _cpuIcon;
    private readonly MetricTrayIcon _ramIcon;
    private readonly MetricTrayIcon _gpuIcon;
    private readonly ContextMenuStrip _menu;

    public TrayApplicationContext()
    {
        _settings = Settings.Load();
        _adapters = GpuAdapterNames.GetAdapters();

        _menu = new ContextMenuStrip();

        var gpuMenu = new ToolStripMenuItem("GPU Source");
        var autoItem = new ToolStripMenuItem("Busiest GPU (auto)") { Checked = _settings.SelectedGpuName is null };
        autoItem.Click += (_, _) => SelectGpu(null, autoItem);
        gpuMenu.DropDownItems.Add(autoItem);
        _gpuMenuItems.Add(autoItem);

        if (_adapters.Count > 0)
        {
            gpuMenu.DropDownItems.Add(new ToolStripSeparator());
            foreach ((string luid, string name) in _adapters)
            {
                bool isSelected = _settings.SelectedGpuName == name;
                var item = new ToolStripMenuItem(name) { Checked = isSelected };
                item.Click += (_, _) => SelectGpu(name, item);
                gpuMenu.DropDownItems.Add(item);
                _gpuMenuItems.Add(item);

                if (isSelected)
                {
                    _selectedLuid = luid;
                }
            }
        }

        _menu.Items.Add(gpuMenu);
        _menu.Items.Add(new ToolStripSeparator());

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => ExitThread();
        _menu.Items.Add(exitItem);

        _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
        _cpuCounter.NextValue();

        _cpuIcon = new MetricTrayIcon("CPU", Color.DeepSkyBlue, SampleCpu, _menu);
        _ramIcon = new MetricTrayIcon("Memory", Color.LimeGreen, SampleMemory, _menu);
        _gpuIcon = new MetricTrayIcon("GPU", Color.OrangeRed, SampleGpu, _menu);

        _cpuIcon.Update();
        _ramIcon.Update();
        _gpuIcon.Update();

        _timer = new System.Windows.Forms.Timer { Interval = 1500 };
        _timer.Tick += (_, _) =>
        {
            _cpuIcon.Update();
            _ramIcon.Update();
            _gpuIcon.Update();
        };
        _timer.Start();
    }

    private void SelectGpu(string? name, ToolStripMenuItem selected)
    {
        foreach (ToolStripMenuItem item in _gpuMenuItems)
        {
            item.Checked = item == selected;
        }
        _settings.SelectedGpuName = name;
        _selectedLuid = name is null ? null : _adapters.FirstOrDefault(a => a.Name == name).Luid;
        _settings.Save();
        _gpuIcon.Update();
    }

    private int SampleCpu() => (int)Math.Round(_cpuCounter.NextValue());

    private int SampleMemory()
    {
        var status = new NativeMethods.MEMORYSTATUSEX
        {
            dwLength = (uint)Marshal.SizeOf<NativeMethods.MEMORYSTATUSEX>(),
        };
        NativeMethods.GlobalMemoryStatusEx(ref status);
        return (int)status.dwMemoryLoad;
    }

    private int SampleGpu()
    {
        string[] instanceNames;
        try
        {
            instanceNames = new PerformanceCounterCategory("GPU Engine").GetInstanceNames();
        }
        catch (InvalidOperationException)
        {
            return 0;
        }

        var currentNames = new HashSet<string>(instanceNames);

        foreach (string name in instanceNames)
        {
            if (!_gpuCounters.ContainsKey(name))
            {
                try
                {
                    _gpuCounters.Add(name, new PerformanceCounter("GPU Engine", "Utilization Percentage", name));
                }
                catch (InvalidOperationException)
                {
                    // Instance vanished between enumeration and open; skip it this tick.
                }
            }
        }

        foreach (string staleName in _gpuCounters.Keys.Where(name => !currentNames.Contains(name)).ToList())
        {
            _gpuCounters[staleName].Dispose();
            _gpuCounters.Remove(staleName);
        }

        // Windows exposes one counter instance per (process, GPU adapter, engine). Task Manager's
        // per-GPU "Utilization" is: sum concurrent processes sharing one (GPU, engine), then take
        // the busiest engine on that GPU. Summing across engine types (the previous bug) or across
        // GPUs inflates the result past what any single GPU is actually doing, e.g. an idle-ish
        // Intel iGPU (46% on its 3D engine) plus a mostly-idle discrete GPU's Copy/VideoDecode
        // engines added together can clamp straight to 100 even though neither GPU is saturated.
        var perEngineSum = new Dictionary<(string Luid, string EngineType), float>();
        foreach (var (name, counter) in _gpuCounters)
        {
            float value;
            try
            {
                value = counter.NextValue();
            }
            catch (InvalidOperationException)
            {
                continue;
            }

            (string luid, string engineType) = ParseGpuEngineInstance(name);
            var key = (luid, engineType);
            perEngineSum[key] = perEngineSum.GetValueOrDefault(key) + value;
        }

        var maxByGpu = new Dictionary<string, float>();
        foreach (var ((luid, _), sum) in perEngineSum)
        {
            float clamped = Math.Min(100f, sum);
            if (!maxByGpu.TryGetValue(luid, out float existing) || clamped > existing)
            {
                maxByGpu[luid] = clamped;
            }
        }

        if (_selectedLuid is { } selectedLuid)
        {
            return (int)Math.Round(maxByGpu.GetValueOrDefault(selectedLuid, 0f));
        }

        float busiestGpu = maxByGpu.Values.DefaultIfEmpty(0f).Max();
        return (int)Math.Round(busiestGpu);
    }

    // Instance names look like "pid_1234_luid_0x00000000_0x0000C3D7_phys_0_eng_0_engtype_3D".
    private static (string Luid, string EngineType) ParseGpuEngineInstance(string instanceName)
    {
        int luidIdx = instanceName.IndexOf("luid_", StringComparison.Ordinal);
        int physIdx = instanceName.IndexOf("_phys_", StringComparison.Ordinal);
        int engtypeIdx = instanceName.IndexOf("engtype_", StringComparison.Ordinal);

        string luid = luidIdx >= 0 && physIdx > luidIdx
            ? instanceName[(luidIdx + "luid_".Length)..physIdx]
            : instanceName;
        string engineType = engtypeIdx >= 0
            ? instanceName[(engtypeIdx + "engtype_".Length)..]
            : instanceName;

        return (luid, engineType);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Stop();
            _timer.Dispose();
            _cpuIcon.Dispose();
            _ramIcon.Dispose();
            _gpuIcon.Dispose();
            _cpuCounter.Dispose();
            foreach (var counter in _gpuCounters.Values)
            {
                counter.Dispose();
            }
            _gpuCounters.Clear();
            _menu.Dispose();
        }
        base.Dispose(disposing);
    }
}
