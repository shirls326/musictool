using System.IO;
using NAudio.Wave;

namespace MusicTool.App.Services;

public sealed class AudioRecordingService : IDisposable
{
    private readonly object captureLock = new();
    private WaveInEvent? input;
    private WaveFileWriter? writer;
    private WaveOutEvent? monitorOutput;
    private BufferedWaveProvider? monitorBuffer;
    private bool isRecording;
    private bool monitorEnabled;
    private long recordedBytes;

    public event EventHandler<RecordingProgressEventArgs>? RecordingProgress;
    public bool IsRecording => isRecording;
    public bool IsMonitoring => monitorEnabled;

    public void Start(string outputPath, bool enableMonitoring)
    {
        if (isRecording)
        {
            throw new InvalidOperationException("A recording is already in progress.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        var existingInput = input;
        var newInput = existingInput is null ? CreateInput() : null;
        var activeInput = existingInput ?? newInput!;
        var newWriter = new WaveFileWriter(outputPath, activeInput.WaveFormat);

        try
        {
            lock (captureLock)
            {
                writer = newWriter;
                recordedBytes = 0;
                isRecording = true;
                if (newInput is not null)
                {
                    input = newInput;
                }
            }

            if (enableMonitoring && !monitorEnabled)
            {
                EnableMonitoring();
            }

            newInput?.StartRecording();
        }
        catch
        {
            newWriter.Dispose();
            lock (captureLock)
            {
                if (ReferenceEquals(writer, newWriter))
                {
                    writer = null;
                    isRecording = false;
                }
            }

            if (newInput is not null)
            {
                DisableMonitoring();
                lock (captureLock)
                {
                    if (ReferenceEquals(input, newInput))
                    {
                        input = null;
                    }
                }

                newInput.Dispose();
            }

            throw;
        }
    }

    public void SetMonitoring(bool enabled)
    {
        if (enabled == monitorEnabled)
        {
            return;
        }

        if (enabled)
        {
            EnableMonitoring();
            return;
        }

        DisableMonitoring();
        if (!isRecording)
        {
            StopCapture();
        }
    }

    public void Stop()
    {
        var activeInput = input;
        WaveFileWriter? activeWriter;
        lock (captureLock)
        {
            activeWriter = writer;
            writer = null;
            isRecording = false;
        }

        if (activeWriter is null)
        {
            return;
        }

        activeWriter.Dispose();
        if (!monitorEnabled)
        {
            StopCapture(activeInput);
        }
    }

    private WaveInEvent CreateInput()
    {
        var capture = new WaveInEvent
        {
            WaveFormat = new WaveFormat(44100, 16, 1),
            BufferMilliseconds = 50,
            NumberOfBuffers = 3
        };
        capture.DataAvailable += OnDataAvailable;
        return capture;
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        RecordingProgressEventArgs? progress = null;
        lock (captureLock)
        {
            if (writer is not null)
            {
                writer.Write(e.Buffer, 0, e.BytesRecorded);
                recordedBytes += e.BytesRecorded;
                var peak = GetPeak(e.Buffer, e.BytesRecorded);
                var durationSeconds = recordedBytes
                    / (double)writer.WaveFormat.AverageBytesPerSecond;
                progress = new RecordingProgressEventArgs(durationSeconds, peak);
            }

            if (monitorEnabled)
            {
                monitorBuffer?.AddSamples(e.Buffer, 0, e.BytesRecorded);
            }
        }

        if (progress is not null)
        {
            RecordingProgress?.Invoke(this, progress);
        }
    }

    private static float GetPeak(byte[] buffer, int bytesRecorded)
    {
        var peak = 0f;
        for (var offset = 0; offset + 1 < bytesRecorded; offset += 2)
        {
            var sample = (short)(buffer[offset] | buffer[offset + 1] << 8);
            peak = Math.Max(peak, Math.Abs(sample / 32768f));
        }

        return peak;
    }

    private void EnableMonitoring()
    {
        var activeInput = input;
        var newInput = activeInput is null ? CreateInput() : null;
        activeInput ??= newInput;
        var buffer = new BufferedWaveProvider(activeInput!.WaveFormat)
        {
            BufferDuration = TimeSpan.FromMilliseconds(500),
            DiscardOnBufferOverflow = true
        };
        var output = new WaveOutEvent { DesiredLatency = 80, NumberOfBuffers = 2 };

        try
        {
            output.Init(buffer);
            output.Play();
            lock (captureLock)
            {
                monitorBuffer = buffer;
                monitorOutput = output;
                monitorEnabled = true;
                if (newInput is not null)
                {
                    input = newInput;
                }
            }

            newInput?.StartRecording();
        }
        catch
        {
            lock (captureLock)
            {
                monitorEnabled = false;
                monitorBuffer = null;
                monitorOutput = null;
                if (ReferenceEquals(input, newInput))
                {
                    input = null;
                }
            }

            output.Dispose();
            newInput?.Dispose();
            throw;
        }
    }

    private void DisableMonitoring()
    {
        WaveOutEvent? output;
        lock (captureLock)
        {
            monitorEnabled = false;
            monitorBuffer = null;
            output = monitorOutput;
            monitorOutput = null;
        }

        try
        {
            output?.Stop();
        }
        finally
        {
            output?.Dispose();
        }
    }

    private void StopCapture(WaveInEvent? expectedInput = null)
    {
        WaveInEvent? activeInput;
        lock (captureLock)
        {
            activeInput = input;
            if (expectedInput is not null && !ReferenceEquals(activeInput, expectedInput))
            {
                return;
            }

            input = null;
        }

        if (activeInput is null)
        {
            return;
        }

        try
        {
            activeInput.StopRecording();
        }
        finally
        {
            activeInput.DataAvailable -= OnDataAvailable;
            activeInput.Dispose();
        }
    }

    public void Dispose()
    {
        Stop();
        DisableMonitoring();
        StopCapture();
    }
}
