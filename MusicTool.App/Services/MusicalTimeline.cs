namespace MusicTool.App.Services;

public static class MusicalTimeline
{
    public static double GetBeatUnitSeconds(int tempoBpm, int denominator)
    {
        if (tempoBpm is < 20 or > 300)
        {
            throw new ArgumentOutOfRangeException(nameof(tempoBpm), "Tempo must be between 20 and 300 BPM.");
        }

        if (denominator is not (4 or 8))
        {
            throw new ArgumentOutOfRangeException(nameof(denominator), "The time-signature denominator must be 4 or 8.");
        }

        return 60d / tempoBpm * 4 / denominator;
    }

    public static double GetSecondsPerBar(int tempoBpm, int numerator, int denominator)
    {
        if (numerator is not (2 or 3 or 4 or 5 or 6 or 7 or 12))
        {
            throw new ArgumentOutOfRangeException(nameof(numerator), "The time-signature numerator is not supported.");
        }

        return GetBeatUnitSeconds(tempoBpm, denominator) * numerator;
    }

    public static string FormatBarBeat(double seconds, int tempoBpm, int numerator, int denominator)
    {
        if (!double.IsFinite(seconds) || seconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(seconds), "Timeline position must be a non-negative time.");
        }

        var secondsPerBar = GetSecondsPerBar(tempoBpm, numerator, denominator);
        var beatUnitSeconds = secondsPerBar / numerator;
        var barIndex = (long)Math.Floor(seconds / secondsPerBar);
        var beatIndex = (int)Math.Floor((seconds - barIndex * secondsPerBar) / beatUnitSeconds);
        return $"Bar {barIndex + 1}, Beat {beatIndex + 1}";
    }
}
