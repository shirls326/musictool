namespace MusicTool.App.Services;

/// <summary>Reports that an open audio stream stopped with an error, and which device it was using.</summary>
public sealed class AudioDeviceFailedEventArgs(string? deviceId, Exception exception) : EventArgs
{
    public string? DeviceId { get; } = deviceId;
    public Exception Exception { get; } = exception;
}
