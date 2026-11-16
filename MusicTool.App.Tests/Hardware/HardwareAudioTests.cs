using System.Diagnostics;
using MusicTool.App.Models;
using MusicTool.App.Services;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using Xunit;
using Xunit.Abstractions;

namespace MusicTool.App.Tests.Hardware;

/// <summary>
/// MT-012 tests that use this PC's real audio devices. Skipped unless MUSICTOOL_HARDWARE_TESTS=1.
/// Take headphones off or turn them down first: the tests play a short, quiet 1 kHz tone.
/// </summary>
[Trait("Category", "Hardware")]
public sealed class HardwareAudioTests : IDisposable
{
    private const int LatencyRepetitions = 5;
    private static readonly TimeSpan ToneTimeout = TimeSpan.FromSeconds(2);
    // After the main output detects the tone, keep listening on the other output for this long.
    private static readonly TimeSpan OtherOutputGrace = TimeSpan.FromMilliseconds(300);

    private readonly ITestOutputHelper log;
    private readonly string tempFolder = Path.Combine(Path.GetTempPath(), $"MusicToolHardwareTests-{Guid.NewGuid():N}");

    public HardwareAudioTests(ITestOutputHelper log)
    {
        this.log = log;
    }

    public void Dispose()
    {
        if (Directory.Exists(tempFolder))
        {
            Directory.Delete(tempFolder, recursive: true);
        }
    }

    [HardwareFact]
    public void PlaybackUsesTheChosenOutput()
    {
        var devices = ResolveDevices();
        var tonePath = ToneSignal.WriteToneWav(tempFolder);

        using var mainProbe = new LoopbackProbe(devices.Output.Id);
        using var otherProbe = devices.OtherOutput is null ? null : new LoopbackProbe(devices.OtherOutput.Id);
        mainProbe.Start();
        otherProbe?.Start();

        using var playback = new AudioPlaybackService();
        playback.SetOutputDevice(devices.Output.Id);
        playback.Play(ToneClip(tonePath));

        Assert.Equal(devices.Output.Id, playback.ActiveOutputDeviceId);
        var detected = mainProbe.WaitForTone(ToneTimeout);
        Thread.Sleep(OtherOutputGrace);
        playback.Stop();

        LogReading(mainProbe);
        Assert.True(detected, $"The tone was not detected on the loopback of '{devices.Output.Name}'. {Describe(mainProbe)}");

        if (otherProbe is null)
        {
            log.WriteLine("Other output not connected: the 'tone is not on the other output' check was omitted.");
            return;
        }

        LogReading(otherProbe);
        Assert.Null(otherProbe.FirstToneTimestamp);
    }

    [HardwareFact]
    public void SystemDefaultOutputFollowsWindows()
    {
        var tonePath = ToneSignal.WriteToneWav(tempFolder);
        var windowsDefault = AudioDeviceCatalog.GetDefault(DataFlow.Render);
        Assert.True(windowsDefault is not null, "Windows has no default output device.");
        log.WriteLine($"Windows default output: '{windowsDefault!.Name}'");

        using var probe = new LoopbackProbe(windowsDefault.Id);
        probe.Start();

        using var playback = new AudioPlaybackService();
        playback.SetOutputDevice(null);
        playback.Play(ToneClip(tonePath));

        Assert.Equal(windowsDefault.Id, playback.ActiveOutputDeviceId);
        var detected = probe.WaitForTone(ToneTimeout);
        playback.Stop();

        LogReading(probe);
        Assert.True(detected, $"The tone was not detected on the Windows default output. {Describe(probe)}");
    }

    [HardwareFact]
    public void RecordingUsesTheChosenMicrophone()
    {
        var devices = ResolveDevices();
        var takePath = Path.Combine(tempFolder, "take.wav");

        using (var recorder = new AudioRecordingService())
        {
            recorder.SetDevices(devices.Mic.Id, devices.Output.Id);
            recorder.Start(takePath, enableMonitoring: false);
            try
            {
                Assert.Equal(devices.Mic.Id, recorder.ActiveInputDeviceId);
                Thread.Sleep(TimeSpan.FromSeconds(1));
            }
            finally
            {
                recorder.Stop();
            }
        }

        using var reader = new WaveFileReader(takePath);
        var format = reader.WaveFormat;
        var duration = reader.TotalTime.TotalSeconds;
        var buffer = new byte[reader.Length];
        var read = reader.Read(buffer, 0, buffer.Length);
        var peak = ToneSignal.ToMono(buffer, read, format).Select(Math.Abs).DefaultIfEmpty(0f).Max();
        log.WriteLine($"Take: {format.SampleRate} Hz, {format.BitsPerSample}-bit, {format.Channels} channel(s), "
            + $"{duration:F3} s, peak {ToDbfs(peak):F1} dBFS");

        Assert.Equal(44100, format.SampleRate);
        Assert.Equal(16, format.BitsPerSample);
        Assert.Equal(1, format.Channels);
        Assert.InRange(duration, 0.8, 1.5);
        Assert.True(peak > 0, "Every sample in the take is zero.");
    }

    [HardwareFact]
    public void MonitoringUsesTheChosenOutput()
    {
        var devices = ResolveDevices();
        using var probe = new LoopbackProbe(devices.Output.Id);
        probe.Start();

        using var recorder = new AudioRecordingService();
        recorder.SetDevices(devices.Mic.Id, devices.Output.Id);
        recorder.SetMonitoring(true);
        try
        {
            Assert.Equal(devices.Output.Id, recorder.ActiveMonitorOutputDeviceId);
            var received = probe.WaitForData(ToneTimeout);
            log.WriteLine($"Loopback on '{probe.DeviceName}' received {probe.BytesReceived} bytes while monitoring.");
            Assert.True(received, $"Loopback on '{devices.Output.Name}' received no audio data while monitoring.");
        }
        finally
        {
            recorder.SetMonitoring(false);
        }
    }

    [HardwareFact]
    public void ChangingDevicesWhileMonitoringRestartsOnTheNewOutput()
    {
        var devices = ResolveDevices();
        Assert.True(devices.OtherOutput is not null,
            "This test needs a second output device to switch to. Connect one or set "
            + $"{TestDevices.OtherOutputVariable} to part of its name.");

        using var recorder = new AudioRecordingService();
        recorder.SetDevices(devices.Mic.Id, devices.Output.Id);
        recorder.SetMonitoring(true);
        try
        {
            Assert.Equal(devices.Output.Id, recorder.ActiveMonitorOutputDeviceId);
            recorder.SetDevices(devices.Mic.Id, devices.OtherOutput!.Id);

            Assert.True(recorder.IsMonitoring, "Monitoring turned off after changing devices.");
            Assert.Equal(devices.OtherOutput.Id, recorder.ActiveMonitorOutputDeviceId);
            Assert.Equal(devices.Mic.Id, recorder.ActiveInputDeviceId);
            log.WriteLine($"Monitoring moved from '{devices.Output.Name}' to '{devices.OtherOutput.Name}'.");
        }
        finally
        {
            recorder.SetMonitoring(false);
        }
    }

    [HardwareFact]
    public void PlaybackLatencyReport()
    {
        var devices = ResolveDevices();
        var tonePath = ToneSignal.WriteToneWav(tempFolder);
        LogEndpointVolume(devices.Output);

        // (a) The app's current path: AudioPlaybackService on shared-mode WASAPI.
        var wasapi = MeasureLatency(devices.Output.Id, start =>
        {
            var playback = new AudioPlaybackService();
            playback.SetOutputDevice(devices.Output.Id);
            start();
            playback.Play(ToneClip(tonePath));
            return playback;
        });
        log.WriteLine($"(a) AudioPlaybackService (WASAPI shared, 200 ms buffer): {Summarize(wasapi)}");

        // (b) The pre-MT-002 path, recreated here only for comparison: a default WaveOutEvent.
        var windowsDefault = AudioDeviceCatalog.GetDefault(DataFlow.Render);
        double[]? waveOut = null;
        if (windowsDefault is null || !string.Equals(windowsDefault.Id, devices.Output.Id, StringComparison.OrdinalIgnoreCase))
        {
            log.WriteLine($"(b) WaveOutEvent: not measured. WaveOutEvent can only use the Windows default output "
                + $"('{windowsDefault?.Name ?? "none"}'), which is not the test output '{devices.Output.Name}'.");
        }
        else
        {
            waveOut = MeasureLatency(devices.Output.Id, start =>
            {
                // Timed from before file and device setup, like the old AudioPlaybackService.Play().
                start();
                var reader = new AudioFileReader(tonePath);
                var player = new WaveOutEvent();
                try
                {
                    player.Init(reader);
                    player.Play();
                    return new CompositeDisposable(player, reader);
                }
                catch
                {
                    player.Dispose();
                    reader.Dispose();
                    throw;
                }
            });
            log.WriteLine($"(b) WaveOutEvent (WinMM, NAudio defaults): {Summarize(waveOut)}");
        }

        Assert.InRange(Median(wasapi), 0, 1000);
        if (waveOut is not null)
        {
            Assert.InRange(Median(waveOut), 0, 1000);
        }
    }

    /// <summary>
    /// Runs one playback per repetition and returns each latency in milliseconds. The callback must
    /// call <c>start()</c> where the app's own <c>Play()</c> call would begin (before any file or device
    /// setup) and return what to dispose.
    /// </summary>
    private double[] MeasureLatency(string outputId, Func<Action, IDisposable> play)
    {
        var results = new double[LatencyRepetitions];
        for (var i = 0; i < LatencyRepetitions; i++)
        {
            using var probe = new LoopbackProbe(outputId);
            probe.Start();
            long startTimestamp = 0;
            using (play(() => startTimestamp = Stopwatch.GetTimestamp()))
            {
                var detected = probe.WaitForTone(ToneTimeout);
                Assert.True(detected, $"Repetition {i + 1}: the tone was not detected. {Describe(probe)}");
            }

            results[i] = probe.ToneLatencySince(startTimestamp)!.Value.TotalMilliseconds;
            log.WriteLine($"    run {i + 1}: {results[i]:F0} ms (tone {probe.StrongestReading.AmplitudeDbfs:F1} dBFS)");
            // Let the stopped stream drain before the next run.
            Thread.Sleep(100);
        }

        return results;
    }

    private TestDeviceSet ResolveDevices()
    {
        var devices = TestDevices.Resolve();
        log.WriteLine($"Microphone:   '{devices.Mic.Name}'");
        log.WriteLine($"Output:       '{devices.Output.Name}'");
        log.WriteLine($"Other output: {(devices.OtherOutput is null ? "(not connected)" : $"'{devices.OtherOutput.Name}'")}");
        return devices;
    }

    private void LogReading(LoopbackProbe probe)
    {
        var reading = probe.StrongestReading;
        log.WriteLine($"Loopback '{probe.DeviceName}' ({probe.Format.SampleRate} Hz, {probe.Format.Channels} ch): "
            + $"detected={probe.FirstToneTimestamp is not null}, strongest 1 kHz {reading.AmplitudeDbfs:F1} dBFS "
            + $"({reading.ToneEnergyFraction:P0} of energy), {probe.BytesReceived} bytes");
    }

    private void LogEndpointVolume(AudioDeviceInfo output)
    {
        // Read only: the tests never change Windows volume, mute, or default devices.
        using var device = AudioDeviceCatalog.OpenDevice(DataFlow.Render, output.Id);
        var volume = device.AudioEndpointVolume;
        log.WriteLine($"Windows volume for '{output.Name}': {volume.MasterVolumeLevelScalar:P0}, muted={volume.Mute} "
            + $"(test tone is {ToDbfs(ToneSignal.Amplitude):F1} dBFS)");
    }

    private static string Describe(LoopbackProbe probe) =>
        $"Strongest 1 kHz reading {probe.StrongestReading.AmplitudeDbfs:F1} dBFS "
        + $"({probe.StrongestReading.ToneEnergyFraction:P0} of energy); {probe.BytesReceived} bytes captured"
        + (probe.CaptureError is null ? "." : $"; capture error: {probe.CaptureError.Message}");

    private static IEnumerable<(AudioTrack Track, AudioClip Clip, string Path)> ToneClip(string tonePath) =>
    [
        (new AudioTrack { Name = "Tone" }, new AudioClip { RelativePath = Path.GetFileName(tonePath) }, tonePath)
    ];

    private static string Summarize(double[] values) =>
        $"median {Median(values):F0} ms, range {values.Min():F0}–{values.Max():F0} ms over {values.Length} runs";

    private static double Median(double[] values)
    {
        var sorted = values.Order().ToArray();
        var middle = sorted.Length / 2;
        return sorted.Length % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
    }

    private static double ToDbfs(double amplitude) => amplitude > 0 ? 20 * Math.Log10(amplitude) : double.NegativeInfinity;

    private sealed class CompositeDisposable(params IDisposable[] items) : IDisposable
    {
        public void Dispose()
        {
            foreach (var item in items)
            {
                item.Dispose();
            }
        }
    }
}
