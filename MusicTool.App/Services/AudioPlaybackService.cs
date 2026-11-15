using MusicTool.App.Models;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace MusicTool.App.Services;

public sealed class AudioPlaybackService : IDisposable
{
    // Shared-mode WASAPI output buffer for project playback (NAudio's default). The input-monitoring
    // constants in AudioRecordingService are separate and intentionally unchanged.
    private const int PlaybackLatencyMilliseconds = 200;
    private static readonly WaveFormat OutputFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
    private readonly List<AudioFileReader> readers = [];
    private WasapiOut? output;
    private MMDevice? outputDevice;
    private string? outputDeviceId;
    // ID of the device actually opened, cached so callers never query a device that has just disappeared.
    private string? openOutputDeviceId;
    private bool stopping;

    public event EventHandler? PlaybackCompleted;

    /// <summary>Raised instead of <see cref="PlaybackCompleted"/> when playback stops because of an error, such as a lost device.</summary>
    public event EventHandler<AudioDeviceFailedEventArgs>? PlaybackFailed;

    public bool IsPlaying => output?.PlaybackState == PlaybackState.Playing;
    public bool IsPaused => output?.PlaybackState == PlaybackState.Paused;

    /// <summary>The ID of the output device currently open for playback, or null when nothing is playing or paused.</summary>
    public string? ActiveOutputDeviceId => output is null ? null : openOutputDeviceId;

    /// <summary>
    /// Selects the output device for later playback. Null means the system default.
    /// Stop playback before changing devices.
    /// </summary>
    public void SetOutputDevice(string? deviceId)
    {
        if (output is not null)
        {
            throw new InvalidOperationException("Stop playback before changing the output device.");
        }

        outputDeviceId = deviceId;
    }

    public static bool HasAudioAtOrAfter(
        IEnumerable<(AudioTrack Track, AudioClip Clip, string Path)> clips,
        double startAtSeconds)
    {
        if (!double.IsFinite(startAtSeconds) || startAtSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(startAtSeconds), "Playback position must be a non-negative time.");
        }

        foreach (var (track, clip, path) in clips)
        {
            if (string.IsNullOrWhiteSpace(clip.RelativePath))
            {
                continue;
            }

            if (!double.IsFinite(clip.StartSeconds) || clip.StartSeconds < 0
                || !double.IsFinite(clip.TrimStartSeconds) || clip.TrimStartSeconds < 0
                || !double.IsFinite(clip.TrimEndSeconds) || clip.TrimEndSeconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(clip), $"A clip on track '{track.Name}' has an invalid time value.");
            }

            using var reader = new AudioFileReader(path);
            var clipDurationSeconds = reader.TotalTime.TotalSeconds
                - clip.TrimStartSeconds - clip.TrimEndSeconds;
            if (clipDurationSeconds <= 0)
            {
                throw new InvalidOperationException($"A clip on track '{track.Name}' has no audio left after trimming.");
            }

            if (clip.StartSeconds + clipDurationSeconds > startAtSeconds)
            {
                return true;
            }
        }

        return false;
    }

    public void Play(IEnumerable<(AudioTrack Track, AudioClip Clip, string Path)> clips, double startAtSeconds = 0)
    {
        if (!double.IsFinite(startAtSeconds) || startAtSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(startAtSeconds), "Playback position must be a non-negative time.");
        }

        Stop();
        var mixer = new MixingSampleProvider(OutputFormat) { ReadFully = false };

        try
        {
            foreach (var (track, clip, path) in clips)
            {
                if (string.IsNullOrWhiteSpace(clip.RelativePath))
                {
                    continue;
                }

                var reader = new AudioFileReader(path);
                readers.Add(reader);
                if (!double.IsFinite(clip.StartSeconds) || clip.StartSeconds < 0
                    || !double.IsFinite(clip.TrimStartSeconds) || clip.TrimStartSeconds < 0
                    || !double.IsFinite(clip.TrimEndSeconds) || clip.TrimEndSeconds < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(clip), $"A clip on track '{track.Name}' has an invalid time value.");
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

                var trimStartSeconds = clip.TrimStartSeconds;
                var clipDurationSeconds = reader.TotalTime.TotalSeconds - trimStartSeconds - clip.TrimEndSeconds;
                if (clipDurationSeconds <= 0)
                {
                    throw new InvalidOperationException($"A clip on track '{track.Name}' has no audio left after trimming.");
                }

                var skipSeconds = Math.Max(0, startAtSeconds - clip.StartSeconds);
                if (skipSeconds >= clipDurationSeconds)
                {
                    reader.Dispose();
                    readers.Remove(reader);
                    continue;
                }

                samples = new OffsetSampleProvider(samples)
                {
                    SkipOver = TimeSpan.FromSeconds(trimStartSeconds + skipSeconds),
                    Take = TimeSpan.FromSeconds(clipDurationSeconds - skipSeconds)
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
                var delaySeconds = Math.Max(0, clip.StartSeconds - startAtSeconds);
                if (delaySeconds > 0)
                {
                    input = new OffsetSampleProvider(volume)
                    {
                        DelayBy = TimeSpan.FromSeconds(delaySeconds)
                    };
                }

                mixer.AddMixerInput(input);
            }

            if (!mixer.MixerInputs.Any())
            {
                throw new InvalidOperationException("There is no audio at the selected playhead position.");
            }

            outputDevice = AudioDeviceCatalog.OpenDevice(DataFlow.Render, outputDeviceId);
            openOutputDeviceId = outputDevice.ID;
            output = new WasapiOut(outputDevice, AudioClientShareMode.Shared, useEventSync: true, PlaybackLatencyMilliseconds);
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
            outputDevice?.Dispose();
            outputDevice = null;
            openOutputDeviceId = null;
            foreach (var reader in readers)
            {
                reader.Dispose();
            }

            readers.Clear();
            stopping = false;
        }
    }

    public void Pause() => output?.Pause();

    public void Resume() => output?.Play();

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        if (!stopping && ReferenceEquals(sender, output))
        {
            if (e.Exception is not null)
            {
                PlaybackFailed?.Invoke(this, new AudioDeviceFailedEventArgs(openOutputDeviceId, e.Exception));
                return;
            }

            PlaybackCompleted?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose() => Stop();
}
