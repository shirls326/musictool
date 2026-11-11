namespace MusicTool.App.Models;

public sealed class MusicProject
{
    public string Name { get; set; } = "Untitled Project";
    public int TempoBpm { get; set; } = 128;
    public int TimeSignatureNumerator { get; set; } = 4;
    public int TimeSignatureDenominator { get; set; } = 4;
    public List<AudioTrack> Tracks { get; set; } = [];
}
