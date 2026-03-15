using System.Text.Json;

namespace TastileDesktop.Services;

/// <summary>
/// Manages application settings persistence.
/// </summary>
public class SettingsService
{
    private static readonly string SettingsDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Tastile");
    private static readonly string SettingsFile =
        Path.Combine(SettingsDir, "settings.json");

    public TastileSettings Current { get; private set; } = new();

    public event EventHandler? SettingsChanged;

    public SettingsService()
    {
        Load();
    }

    public void Load()
    {
        if (!File.Exists(SettingsFile))
        {
            Current = new TastileSettings();
            return;
        }
        try
        {
            var json = File.ReadAllText(SettingsFile);
            Current = JsonSerializer.Deserialize<TastileSettings>(json) ?? new();
        }
        catch
        {
            Current = new TastileSettings();
        }
    }

    public void Save(TastileSettings settings)
    {
        Current = settings;
        Directory.CreateDirectory(SettingsDir);
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsFile, json);
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Update(Action<TastileSettings> update)
    {
        var updated = Current with { };
        update(updated);
        Save(updated);
    }
}

/// <summary>
/// Application settings.
/// </summary>
public record TastileSettings
{
    public int ToastNotifyMinutes { get; set; } = 15;
    public int InterventionMinutes { get; set; } = 25;
    public int DefaultBreakMinutes { get; set; } = 5;
    public int IdlePromptMinutes { get; set; } = 5;
    public int InterventionRepeatMinutes { get; set; } = 5;
    public bool LaunchAtStartup { get; set; } = false;
}
