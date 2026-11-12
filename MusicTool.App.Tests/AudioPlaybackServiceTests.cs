using MusicTool.App.Models;
using MusicTool.App.Services;
using NAudio.Wave;
using Xunit;

namespace MusicTool.App.Tests;

public sealed class AudioPlaybackServiceTests
{
    [Fact]
    public void HasAudioAtOrAfterUsesClipTimelineAndTrimBoundaries()
    {
        var root = Path.Combine(Path.GetTempPath(), $"MusicToolTests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "backing.wav");
            CreateWaveFile(path, durationSeconds: 1);
            var clips = new[]
            {
                (
                    Track: new AudioTrack { Name = "Backing" },
                    Clip: new AudioClip
                    {
                        RelativePath = "backing.wav",
                        StartSeconds = 2,
                        TrimStartSeconds = 0.25,
                        TrimEndSeconds = 0.25
                    },
                    Path: path)
            };

            Assert.True(AudioPlaybackService.HasAudioAtOrAfter(clips, 1.99));
            Assert.True(AudioPlaybackService.HasAudioAtOrAfter(clips, 2));
            Assert.True(AudioPlaybackService.HasAudioAtOrAfter(clips, 2.499));
            Assert.False(AudioPlaybackService.HasAudioAtOrAfter(clips, 2.51));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void CreateWaveFile(string path, double durationSeconds)
    {
        var sampleCount = (int)(durationSeconds * 44100);
        using var writer = new WaveFileWriter(path, WaveFormat.CreateIeeeFloatWaveFormat(44100, 1));
        writer.WriteSamples(new float[sampleCount], 0, sampleCount);
    }
}
