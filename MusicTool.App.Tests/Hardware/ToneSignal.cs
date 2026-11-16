using NAudio.Wave;

namespace MusicTool.App.Tests.Hardware;

/// <summary>One Goertzel measurement of the test frequency.</summary>
/// <param name="Amplitude">Estimated peak amplitude of the tone (1.0 = full scale).</param>
/// <param name="ToneEnergyFraction">Share of the block's total energy that is at the test frequency (0–1).</param>
public readonly record struct ToneReading(double Amplitude, double ToneEnergyFraction)
{
    public bool Detected =>
        Amplitude >= ToneSignal.MinDetectAmplitude && ToneEnergyFraction >= ToneSignal.MinToneEnergyFraction;

    public double AmplitudeDbfs => Amplitude > 0 ? 20 * Math.Log10(Amplitude) : double.NegativeInfinity;
}

/// <summary>
/// Generates the 1 kHz test tone and detects it with the Goertzel algorithm, which measures
/// the strength of a single frequency without a full FFT.
/// </summary>
public static class ToneSignal
{
    public const double FrequencyHz = 1000;

    /// <summary>About −20 dBFS: quiet, but well above the detection floor.</summary>
    public const float Amplitude = 0.1f;

    /// <summary>About −40 dBFS. Below this the tone counts as absent.</summary>
    public const double MinDetectAmplitude = 0.01;

    /// <summary>The tone must make up at least half of a block's energy, so broadband sound can't pass.</summary>
    public const double MinToneEnergyFraction = 0.5;

    /// <summary>Length of each analysis block (about one loopback buffer): 10 cycles of 1 kHz.</summary>
    public const double BlockSeconds = 0.010;

    private const double FadeSeconds = 0.002;

    // KSDATAFORMAT_SUBTYPE_IEEE_FLOAT, used by WAVE_FORMAT_EXTENSIBLE mix formats.
    private static readonly Guid IeeeFloatSubFormat = new("00000003-0000-0010-8000-00aa00389b71");
    private static readonly Guid PcmSubFormat = new("00000001-0000-0010-8000-00aa00389b71");

    public static int BlockLength(int sampleRate) => (int)Math.Round(sampleRate * BlockSeconds);

    /// <summary>A mono sine with short fades at each end so it doesn't click.</summary>
    public static float[] Generate(double frequencyHz, double durationSeconds, int sampleRate, float amplitude)
    {
        var samples = new float[(int)Math.Round(durationSeconds * sampleRate)];
        var fadeLength = Math.Max(1, (int)(FadeSeconds * sampleRate));
        for (var i = 0; i < samples.Length; i++)
        {
            var fade = Math.Min(1.0, Math.Min(i, samples.Length - 1 - i) / (double)fadeLength);
            samples[i] = (float)(amplitude * fade * Math.Sin(2 * Math.PI * frequencyHz * i / sampleRate));
        }

        return samples;
    }

    /// <summary>Writes the 1 kHz test tone as a 44.1 kHz, 16-bit mono WAV in <paramref name="folder"/>.</summary>
    public static string WriteToneWav(string folder, double durationSeconds = 0.8)
    {
        const int sampleRate = 44100;
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "tone-1khz.wav");
        var samples = Generate(FrequencyHz, durationSeconds, sampleRate, Amplitude);
        using var writer = new WaveFileWriter(path, new WaveFormat(sampleRate, 16, 1));
        writer.WriteSamples(samples, 0, samples.Length);
        return path;
    }

    /// <summary>
    /// Goertzel measurement of <see cref="FrequencyHz"/> over one block of mono samples.
    /// A Hann window keeps nearby frequencies (such as 440 Hz) from leaking into the reading.
    /// </summary>
    public static ToneReading Measure(ReadOnlySpan<float> mono, int sampleRate, double frequencyHz = FrequencyHz)
    {
        if (mono.Length < 2)
        {
            return default;
        }

        var coefficient = 2 * Math.Cos(2 * Math.PI * frequencyHz / sampleRate);
        double s1 = 0, s2 = 0, windowSum = 0, energy = 0;
        for (var n = 0; n < mono.Length; n++)
        {
            var window = 0.5 * (1 - Math.Cos(2 * Math.PI * n / (mono.Length - 1)));
            windowSum += window;
            energy += mono[n] * (double)mono[n];
            var s = mono[n] * window + coefficient * s1 - s2;
            s2 = s1;
            s1 = s;
        }

        var power = Math.Max(0, s1 * s1 + s2 * s2 - coefficient * s1 * s2);
        var amplitude = 2 * Math.Sqrt(power) / windowSum;
        var meanSquare = energy / mono.Length;
        // A sine of amplitude A has mean square A²/2.
        var fraction = meanSquare > 0 ? Math.Min(1, amplitude * amplitude / 2 / meanSquare) : 0;
        return new ToneReading(amplitude, fraction);
    }

    /// <summary>
    /// Measures each block of <see cref="BlockSeconds"/> in a mono buffer. Returns the strongest
    /// reading and the sample index where the first detecting block starts (or -1).
    /// </summary>
    public static (ToneReading Strongest, int FirstDetectedIndex) Scan(ReadOnlySpan<float> mono, int sampleRate)
    {
        var blockLength = BlockLength(sampleRate);
        var strongest = default(ToneReading);
        var firstDetected = -1;
        for (var start = 0; start + blockLength <= mono.Length; start += blockLength)
        {
            var reading = Measure(mono.Slice(start, blockLength), sampleRate);
            if (reading.Amplitude > strongest.Amplitude)
            {
                strongest = reading;
            }

            if (firstDetected < 0 && reading.Detected)
            {
                firstDetected = start;
            }
        }

        return (strongest, firstDetected);
    }

    /// <summary>
    /// Converts a 32-bit float or 16-bit PCM buffer (any channel count) to mono floats by
    /// averaging the channels.
    /// </summary>
    public static float[] ToMono(byte[] buffer, int byteCount, WaveFormat format)
    {
        var isFloat = IsIeeeFloat(format);
        if (!(isFloat && format.BitsPerSample == 32) && !(IsPcm(format) && format.BitsPerSample == 16))
        {
            throw new NotSupportedException(
                $"Tone detection supports 32-bit float or 16-bit PCM audio, not {format.Encoding} {format.BitsPerSample}-bit.");
        }

        var channels = format.Channels;
        var bytesPerSample = format.BitsPerSample / 8;
        var frames = byteCount / (bytesPerSample * channels);
        var mono = new float[frames];
        for (var frame = 0; frame < frames; frame++)
        {
            double sum = 0;
            for (var channel = 0; channel < channels; channel++)
            {
                var offset = (frame * channels + channel) * bytesPerSample;
                sum += isFloat
                    ? BitConverter.ToSingle(buffer, offset)
                    : BitConverter.ToInt16(buffer, offset) / 32768.0;
            }

            mono[frame] = (float)(sum / channels);
        }

        return mono;
    }

    private static bool IsIeeeFloat(WaveFormat format) =>
        format.Encoding == WaveFormatEncoding.IeeeFloat
        || (format is WaveFormatExtensible extensible && extensible.SubFormat == IeeeFloatSubFormat);

    private static bool IsPcm(WaveFormat format) =>
        format.Encoding == WaveFormatEncoding.Pcm
        || (format is WaveFormatExtensible extensible && extensible.SubFormat == PcmSubFormat);
}
