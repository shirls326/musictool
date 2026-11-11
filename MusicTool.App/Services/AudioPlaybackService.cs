using MusicTool.App.Models;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace MusicTool.App.Services;

public sealed class AudioPlaybackService : IDisposable
{
    private static readonly WaveFormat OutputFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
    private readonly List<AudioFileReader> readers = [];
    private WaveOutEvent? output;
    private bool stopping;

    public event EventHandler? PlaybackCompleted;
    public bool IsPlaying => output?.PlaybackState == PlaybackState.Playing;

    public void Play(IEnumerable<(AudioTrack Track, string Path)> tracks)
    {
        Stop();
        var mixer = new MixingSampleProvider(OutputFormat) { ReadFully = false };

        try
        {
            foreach (var (track, path) in tracks)
            {
                if (string.IsNullOrWhiteSpace(track.RelativePath))
                {
                    continue;
                }

                var reader = new AudioFileReader(path);
                readers.Add(reader);
                if (!double.IsFinite(track.StartSeconds) || track.StartSeconds < 0
                    || !double.IsFinite(track.TrimStartSeconds) || track.TrimStartSeconds < 0
                    || !double.IsFinite(track.TrimEndSeconds) || track.TrimEndSeconds < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(track), $"Track '{track.Name}' has an invalid time value.");
                }

                ISampleProvider samples = reader;

                if (samples.WaveFormat.Channels > 2)
                {
                    throw new NotSupportedException("Audio files with more than two channels are not supported yet.");
                }

                if (samples.WaveFormat.SampleRate != OutputFormat.SampleRate)
                {
                    samples = new WdlResamplingSampleProvider(samples, OutputFormat.SampleRate);
                }

                var trimStart = TimeSpan.FromSeconds(track.TrimStartSeconds);
                var trimEnd = TimeSpan.FromSeconds(track.TrimEndSeconds);
                var clipDuration = reader.TotalTime - trimStart - trimEnd;
                if (clipDuration <= TimeSpan.Zero)
                {
                    throw new InvalidOperationException($"Track '{track.Name}' has no audio left after trimming.");
                }

                samples = new OffsetSampleProvider(samples)
                {
                    SkipOver = trimStart,
                    Take = clipDuration
                };

                if (samples.WaveFormat.Channels == 1)
                {
                    samples = new MonoToStereoSampleProvider(samples);
                }

                var volume = new VolumeSampleProvider(samples)
                {
                    Volume = track.IsMuted ? 0 : (float)Math.Clamp(track.Volume, 0, 1)
                };
                ISampleProvider input = volume;
                if (track.StartSeconds > 0)
                {
                    input = new OffsetSampleProvider(volume)
                    {
                        DelayBy = TimeSpan.FromSeconds(track.StartSeconds)
                    };
                }

                mixer.AddMixerInput(input);
            }

            if (readers.Count == 0)
            {
                throw new InvalidOperationException("Add an audio file before pressing Play.");
            }

            output = new WaveOutEvent();
            output.PlaybackStopped += OnPlaybackStopped;
            output.Init(new SampleToWaveProvider(mixer));
            output.Play();
        }
        catch
        {
            Stop();
            throw;
        }
    }

    public void Stop()
    {
        var player = output;
        output = null;
        stopping = true;
        try
        {
            player?.Stop();
        }
        finally
        {
            if (player is not null)
            {
                player.PlaybackStopped -= OnPlaybackStopped;
            }

            player?.Dispose();
            foreach (var reader in readers)
            {
                reader.Dispose();
            }

            readers.Clear();
            stopping = false;
        }
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        if (!stopping && ReferenceEquals(sender, output))
        {
            PlaybackCompleted?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose() => Stop();
}
