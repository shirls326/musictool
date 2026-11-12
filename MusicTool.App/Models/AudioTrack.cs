namespace MusicTool.App.Models;

public sealed class AudioTrack
{
    public string Name { get; set; } = string.Empty;
    public List<AudioClip> Clips { get; set; } = [];

    // Legacy project fields are migrated into Clips when a project is opened.
    public string RelativePath { get; set; } = string.Empty;
    public double StartSeconds { get; set; }
    public double TrimStartSeconds { get; set; }
    public double TrimEndSeconds { get; set; }
    public double Volume { get; set; } = 1;
    public bool IsMuted { get; set; }
}
