using MusicTool.App.Services;
using Xunit;

namespace MusicTool.App.Tests;

public sealed class MusicalTimelineTests
{
    [Fact]
    public void CalculatesBarDurationFromTempoAndTimeSignature()
    {
        Assert.Equal(1.875, MusicalTimeline.GetSecondsPerBar(128, 4, 4), precision: 3);
        Assert.Equal(1.5, MusicalTimeline.GetSecondsPerBar(120, 6, 8), precision: 3);
    }

    [Fact]
    public void FormatsTimelinePositionAsOneBasedBarAndBeat()
    {
        Assert.Equal("Bar 2, Beat 1", MusicalTimeline.FormatBarBeat(1.875, 128, 4, 4));
        Assert.Equal("Bar 1, Beat 2", MusicalTimeline.FormatBarBeat(0.5, 120, 4, 4));
    }
}
