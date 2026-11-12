namespace MusicTool.App.Models;

public sealed class AudioClip
{
    public string RelativePath { get; set; } = string.Empty;
    public double StartSeconds { get; set; }
    public double TrimStartSeconds { get; set; }
    public double TrimEndSeconds { get; set; }
}
