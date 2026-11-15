using System.IO;
using System.Text.Json;
using MusicTool.App.Models;

namespace MusicTool.App.Services;

/// <summary>
/// Loads and saves app preferences in %LOCALAPPDATA%\MusicTool\settings.json.
/// </summary>
public sealed class AppSettingsStore
{
    private const string SettingsFileName = "settings.json";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public AppSettingsStore(string? settingsRoot = null)
    {
        SettingsFilePath = Path.Combine(Path.GetFullPath(settingsRoot ?? GetSettingsRoot()), SettingsFileName);
    }

    public string SettingsFilePath { get; }

    /// <summary>
    /// Returns the saved preferences, or defaults when the file is missing, unreadable, or corrupt.
    /// Never throws, so a bad settings file cannot block startup.
    /// </summary>
    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsFilePath))
            {
                return new AppSettings();
            }

            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsFilePath), JsonOptions)
                ?? new AppSettings();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return new AppSettings();
        }
    }

    /// <summary>
    /// Writes to a temporary file and then replaces settings.json, so an interrupted write
    /// cannot leave a half-written settings file behind.
    /// </summary>
    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsFilePath)!);
        var temporaryPath = $"{SettingsFilePath}.tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings, JsonOptions));
        File.Move(temporaryPath, SettingsFilePath, overwrite: true);
    }

    private static string GetSettingsRoot() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MusicTool");
}
