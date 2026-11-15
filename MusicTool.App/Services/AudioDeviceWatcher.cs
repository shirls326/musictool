using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace MusicTool.App.Services;

/// <summary>
/// Listens for Windows endpoint notifications and reports when an audio device stops being available.
/// Event-driven WASAPI streams may simply stall when a USB device is unplugged instead of reporting
/// an error, so these notifications are the reliable disconnect signal.
/// </summary>
/// <remarks>
/// Windows calls these methods on a background thread. Handlers must not call back into audio APIs
/// synchronously; marshal to the UI thread instead.
/// </remarks>
public sealed class AudioDeviceWatcher : IMMNotificationClient, IDisposable
{
    private readonly MMDeviceEnumerator enumerator = new();
    private bool registered;

    public AudioDeviceWatcher()
    {
        enumerator.RegisterEndpointNotificationCallback(this);
        registered = true;
    }

    /// <summary>Raised with the device ID when a device is removed, unplugged, or disabled.</summary>
    public event EventHandler<string>? DeviceUnavailable;

    void IMMNotificationClient.OnDeviceStateChanged(string deviceId, DeviceState newState)
    {
        if (newState != DeviceState.Active)
        {
            DeviceUnavailable?.Invoke(this, deviceId);
        }
    }

    void IMMNotificationClient.OnDeviceRemoved(string deviceId) => DeviceUnavailable?.Invoke(this, deviceId);

    void IMMNotificationClient.OnDeviceAdded(string pwstrDeviceId)
    {
    }

    void IMMNotificationClient.OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
    {
    }

    void IMMNotificationClient.OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key)
    {
    }

    public void Dispose()
    {
        if (registered)
        {
            registered = false;
            enumerator.UnregisterEndpointNotificationCallback(this);
        }

        enumerator.Dispose();
    }
}
