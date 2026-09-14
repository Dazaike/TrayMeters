using System.Text.Json;

namespace TrayMeters;

/// <summary>Persisted user preferences. Currently just which GPU adapter the GPU tray icon tracks.</summary>
internal sealed class Settings
{
    /// <summary>
    /// LUID key (matching <see cref="GpuAdapterNames.GetAdapters"/>) of the adapter to always show,
    /// or <c>null</c> to track whichever installed GPU is currently busiest.
    /// </summary>
    public string? SelectedGpuLuid { get; set; }

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TrayMeters",
        "settings.json");

    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                string json = File.ReadAllText(FilePath);
                Settings? settings = JsonSerializer.Deserialize<Settings>(json);
                if (settings is not null)
                {
                    return settings;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Missing, corrupt, or inaccessible settings file — fall back to defaults.
        }

        return new Settings();
    }

    public void Save()
    {
        try
        {
            string dir = Path.GetDirectoryName(FilePath)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort; the selection simply won't survive a restart.
        }
    }
}
