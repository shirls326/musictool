namespace MusicTool.App.Models;

public sealed class MusicProject
{
    public string Name { get; set; } = "Untitled Project";
    public List<AudioTrack> Tracks { get; set; } = [];
}
