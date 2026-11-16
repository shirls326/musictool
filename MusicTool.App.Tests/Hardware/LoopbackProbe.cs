using System.Diagnostics;
using MusicTool.App.Services;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace MusicTool.App.Tests.Hardware;

/// <summary>
/// Records what one output device is playing (WASAPI loopback) and watches for the 1 kHz test tone.
/// Start the probe before starting playback, then wait for the tone or a timeout.
/// The capture is always stopped and disposed by <see cref="Dispose"/>.
/// </summary>
public sealed class LoopbackProbe : IDisposable
{
    private readonly object stateLock = new();
    private readonly MMDevice device;
    private readonly LowLatencyLoopbackCapture capture;
    private readonly int sampleRate;
    private readonly int blockLength;
    private readonly List<float> pending = [];
    private readonly ManualResetEventSlim toneDetected = new();
    private readonly ManualResetEventSlim dataReceived = new();
    private ToneReading strongest;
    private long? firstToneTimestamp;
    private long bytesReceived;
    private bool started;
    private volatile bool disposed;

    public LoopbackProbe(string outputDeviceId)
    {
        device = AudioDeviceCatalog.OpenDevice(DataFlow.Render, outputDeviceId);
        try
        {
            DeviceName = device.FriendlyName;
            capture = new LowLatencyLoopbackCapture(device);
            Format = capture.WaveFormat;
            sampleRate = Format.SampleRate;
            blockLength = ToneSignal.BlockLength(sampleRate);
            capture.DataAvailable += OnDataAvailable;
            capture.RecordingStopped += OnRecordingStopped;
        }
        catch
        {
            device.Dispose();
            throw;
        }
    }

    public string DeviceName { get; }
    public WaveFormat Format { get; }

    /// <summary>Set if the capture stopped with an error, such as the device disappearing.</summary>
    public Exception? CaptureError { get; private set; }

    /// <summary><see cref="Stopwatch"/> timestamp of the loopback buffer in which the tone was first detected.</summary>
    public long? FirstToneTimestamp
    {
        get { lock (stateLock) { return firstToneTimestamp; } }
    }

    /// <summary>The strongest 1 kHz reading seen so far, whether or not it counted as detected.</summary>
    public ToneReading StrongestReading
    {
        get { lock (stateLock) { return strongest; } }
    }

    public long BytesReceived => Interlocked.Read(ref bytesReceived);

    public void Start()
    {
        capture.StartRecording();
        started = true;
        // Give the capture thread time to begin polling before the caller starts playback.
        Thread.Sleep(50);
    }

    public bool WaitForTone(TimeSpan timeout) => toneDetected.Wait(timeout);

    public bool WaitForData(TimeSpan timeout) => dataReceived.Wait(timeout);

    /// <summary>Time from <paramref name="startTimestamp"/> to the first tone detection, or null.</summary>
    public TimeSpan? ToneLatencySince(long startTimestamp) =>
        FirstToneTimestamp is { } detected ? Stopwatch.GetElapsedTime(startTimestamp, detected) : null;

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        var arrived = Stopwatch.GetTimestamp();
        if (disposed || e.BytesRecorded <= 0)
        {
            return;
        }

        Interlocked.Add(ref bytesReceived, e.BytesRecorded);
        dataReceived.Set();
        var mono = ToneSignal.ToMono(e.Buffer, e.BytesRecorded, Format);
        lock (stateLock)
        {
            // Carry partial blocks over to the next buffer so every block has the same length.
            pending.AddRange(mono);
            var offset = 0;
            while (pending.Count - offset >= blockLength)
            {
                var block = pending.GetRange(offset, blockLength).ToArray();
                offset += blockLength;
                var reading = ToneSignal.Measure(block, sampleRate);
                if (reading.Amplitude > strongest.Amplitude)
                {
                    strongest = reading;
                }

                if (reading.Detected && firstToneTimestamp is null)
                {
                    firstToneTimestamp = arrived;
                    toneDetected.Set();
                }
            }

            pending.RemoveRange(0, offset);
        }
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception is not null)
        {
            CaptureError = e.Exception;
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        try
        {
            if (started)
            {
                capture.StopRecording();
            }
        }
        finally
        {
            capture.DataAvailable -= OnDataAvailable;
            capture.RecordingStopped -= OnRecordingStopped;
            capture.Dispose();
            device.Dispose();
            toneDetected.Dispose();
            dataReceived.Dispose();
        }
    }

    /// <summary>
    /// The same WASAPI loopback stream that NAudio's <c>WasapiLoopbackCapture</c> opens, but with a
    /// 20 ms buffer. <c>WasapiLoopbackCapture</c> has a fixed 100 ms buffer, so it only checks for new
    /// audio every ~50 ms. This version checks every ~10 ms, matching MT-012's timing resolution.
    /// </summary>
    private sealed class LowLatencyLoopbackCapture(MMDevice renderDevice)
        : WasapiCapture(renderDevice, useEventSync: false, audioBufferMillisecondsLength: 20)
    {
        protected override AudioClientStreamFlags GetAudioClientStreamFlags() =>
            base.GetAudioClientStreamFlags() | AudioClientStreamFlags.Loopback;
    }
}
