using MusicTool.App.Tests.Hardware;
using NAudio.Wave;
using Xunit;

namespace MusicTool.App.Tests;

/// <summary>Non-hardware checks of the MT-012 tone generator and Goertzel detector.</summary>
public sealed class ToneSignalTests
{
    [Theory]
    [InlineData(44100)]
    [InlineData(48000)]
    public void DetectsOneKilohertzInAOneKilohertzBuffer(int sampleRate)
    {
        var tone = ToneSignal.Generate(1000, 0.1, sampleRate, ToneSignal.Amplitude);

        var (strongest, firstDetected) = ToneSignal.Scan(tone, sampleRate);

        Assert.True(strongest.Detected);
        Assert.Equal(0, firstDetected);
        Assert.InRange(strongest.Amplitude, 0.09, 0.11);
    }

    [Fact]
    public void DoesNotDetectOneKilohertzInSilence()
    {
        var (strongest, firstDetected) = ToneSignal.Scan(new float[4800], 48000);

        Assert.False(strongest.Detected);
        Assert.Equal(-1, firstDetected);
    }

    [Fact]
    public void DoesNotDetectOneKilohertzInA440HertzBuffer()
    {
        // Full scale, so any leakage into the 1 kHz reading would be as large as possible.
        var tone = ToneSignal.Generate(440, 0.1, 48000, 1.0f);

        var (strongest, firstDetected) = ToneSignal.Scan(tone, 48000);

        Assert.False(strongest.Detected);
        Assert.Equal(-1, firstDetected);
        Assert.True(strongest.Amplitude < ToneSignal.MinDetectAmplitude);
    }

    [Fact]
    public void FindsWhereTheToneStartsAfterSilence()
    {
        const int sampleRate = 48000;
        var buffer = new float[sampleRate / 10];
        var tone = ToneSignal.Generate(1000, 0.05, sampleRate, ToneSignal.Amplitude);
        tone.CopyTo(buffer, buffer.Length - tone.Length);

        var (_, firstDetected) = ToneSignal.Scan(buffer, sampleRate);

        Assert.Equal(buffer.Length - tone.Length, firstDetected);
    }

    [Fact]
    public void DetectsToneInStereo32BitFloatBytes()
    {
        var mono = ToneSignal.Generate(1000, 0.05, 48000, ToneSignal.Amplitude);
        var bytes = new byte[mono.Length * 2 * sizeof(float)];
        for (var i = 0; i < mono.Length; i++)
        {
            BitConverter.TryWriteBytes(bytes.AsSpan(i * 8), mono[i]);
            BitConverter.TryWriteBytes(bytes.AsSpan(i * 8 + 4), mono[i]);
        }

        var converted = ToneSignal.ToMono(bytes, bytes.Length, WaveFormat.CreateIeeeFloatWaveFormat(48000, 2));

        Assert.Equal(mono.Length, converted.Length);
        Assert.True(ToneSignal.Scan(converted, 48000).Strongest.Detected);
    }

    [Fact]
    public void ToneWavIsDetectedWhenReadAs16BitPcm()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"MusicToolTests-{Guid.NewGuid():N}");
        try
        {
            var path = ToneSignal.WriteToneWav(folder);
            using var reader = new WaveFileReader(path);
            var bytes = new byte[reader.Length];
            var read = reader.Read(bytes, 0, bytes.Length);

            Assert.Equal(new WaveFormat(44100, 16, 1), reader.WaveFormat);
            Assert.InRange(reader.TotalTime.TotalSeconds, 0.79, 0.81);
            var (strongest, _) = ToneSignal.Scan(ToneSignal.ToMono(bytes, read, reader.WaveFormat), 44100);
            Assert.True(strongest.Detected);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
