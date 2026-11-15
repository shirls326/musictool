namespace MusicTool.App.Models;

/// <summary>
/// App-wide preferences stored outside any project. A null device ID means "System default".
/// Device names are kept only for display and for the "isn't connected" notice.
/// </summary>
public sealed class AppSettings
{
    public string? InputDeviceId { get; set; }
    public string? InputDeviceName { get; set; }
    public string? OutputDeviceId { get; set; }
    public string? OutputDeviceName { get; set; }
}
