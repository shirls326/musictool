namespace MusicTool.App.Services;

public sealed class RecordingProgressEventArgs(double durationSeconds, float peak) : EventArgs
{
    public double DurationSeconds { get; } = durationSeconds;
    public float Peak { get; } = peak;
}
