using System.Diagnostics;
using System.Runtime.InteropServices;

namespace TrayMeters;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly System.Windows.Forms.Timer _timer;
    private readonly PerformanceCounter _cpuCounter;
    private readonly PerformanceCounterCategory _gpuCategory = new("GPU Engine");
    private Dictionary<string, CounterSample> _gpuSamples = new();
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
        InstanceDataCollection data;
        try
        {
            // One bulk read of the whole category instead of one PerformanceCounter per
            // (process, engine) instance, which costs hundreds of handles and reads per tick.
            data = _gpuCategory.ReadCategory()["Utilization Percentage"];
        }
        catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException)
        {
            return 0;
        }

        var current = new Dictionary<string, CounterSample>(data.Count);
        var perEngineSum = new Dictionary<(string Luid, string EngineType), float>();
        foreach (InstanceData inst in data.Values)
        {
            CounterSample sample = inst.Sample;
            current[inst.InstanceName] = sample;
            if (!_gpuSamples.TryGetValue(inst.InstanceName, out CounterSample prev))
            {
                continue;
            }

            float value = CounterSample.Calculate(prev, sample);
            (string luid, string engineType) = ParseGpuEngineInstance(inst.InstanceName);
            var key = (luid, engineType);
            perEngineSum[key] = perEngineSum.GetValueOrDefault(key) + value;
        }

        _gpuSamples = current;

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
            _gpuSamples.Clear();
            _menu.Dispose();
        }
        base.Dispose(disposing);
    }
}
