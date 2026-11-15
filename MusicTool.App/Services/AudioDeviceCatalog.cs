using NAudio.CoreAudioApi;

namespace MusicTool.App.Services;

public sealed record AudioDeviceInfo(string Id, string Name);

/// <summary>
/// The outcome of matching a saved device preference against the connected devices.
/// <see cref="DeviceId"/> is null when the system default device should be used.
/// <see cref="FellBack"/> is true when a specific device was saved but is not connected.
/// </summary>
public sealed record DeviceResolution(string? DeviceId, bool FellBack);

/// <summary>
/// Lists and opens WASAPI audio endpoints, and resolves saved device preferences.
/// </summary>
public static class AudioDeviceCatalog
{
    public static IReadOnlyList<AudioDeviceInfo> ListActive(DataFlow flow)
    {
        using var enumerator = new MMDeviceEnumerator();
        var devices = new List<AudioDeviceInfo>();
        foreach (var device in enumerator.EnumerateAudioEndPoints(flow, DeviceState.Active))
        {
            using (device)
            {
                devices.Add(new AudioDeviceInfo(device.ID, device.FriendlyName));
            }
        }

        return devices;
    }

    /// <summary>Returns the current Windows default device, or null when none exists.</summary>
    public static AudioDeviceInfo? GetDefault(DataFlow flow)
    {
        using var enumerator = new MMDeviceEnumerator();
        if (!enumerator.HasDefaultAudioEndpoint(flow, Role.Console))
        {
            return null;
        }

        using var device = enumerator.GetDefaultAudioEndpoint(flow, Role.Console);
        return new AudioDeviceInfo(device.ID, device.FriendlyName);
    }

    /// <summary>
    /// Pure resolution logic (no hardware access): use the saved device if it is connected,
    /// otherwise fall back to the system default and report that a fallback happened.
    /// </summary>
    public static DeviceResolution Resolve(string? savedId, IReadOnlyList<AudioDeviceInfo> activeDevices)
    {
        ArgumentNullException.ThrowIfNull(activeDevices);
        if (string.IsNullOrEmpty(savedId))
        {
            return new DeviceResolution(null, FellBack: false);
        }

        return activeDevices.Any(device => string.Equals(device.Id, savedId, StringComparison.OrdinalIgnoreCase))
            ? new DeviceResolution(savedId, FellBack: false)
            : new DeviceResolution(null, FellBack: true);
    }

    /// <summary>
    /// Opens the device with the given ID, or the system default when the ID is null.
    /// The caller owns and must dispose the returned device.
    /// </summary>
    public static MMDevice OpenDevice(DataFlow flow, string? deviceId)
    {
        var kind = flow == DataFlow.Capture ? "microphone" : "output device";
        using var enumerator = new MMDeviceEnumerator();
        if (deviceId is null)
        {
            if (!enumerator.HasDefaultAudioEndpoint(flow, Role.Console))
            {
                throw new InvalidOperationException($"No {kind} is available. Connect one or choose another in Audio settings.");
            }

            return enumerator.GetDefaultAudioEndpoint(flow, Role.Console);
        }

        MMDevice device;
        try
        {
            device = enumerator.GetDevice(deviceId);
        }
        catch (Exception exception) when (exception is System.Runtime.InteropServices.COMException or ArgumentException)
        {
            throw new InvalidOperationException(
                $"The selected {kind} isn't available. Reconnect it or choose another in Audio settings.", exception);
        }

        if (device.State != DeviceState.Active)
        {
            var name = device.FriendlyName;
            device.Dispose();
            throw new InvalidOperationException(
                $"The selected {kind} '{name}' isn't connected. Reconnect it or choose another in Audio settings.");
        }

        return device;
    }
}
