using System.IO;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace MusicTool.App.Services;

public sealed class AudioRecordingService : IDisposable
{
    private const int CaptureBufferMilliseconds = 20;
    private const int MonitorLatencyMilliseconds = 20;
    private const int MonitorBufferMilliseconds = 100;
    private readonly object captureLock = new();
    private WasapiCapture? input;
    private MMDevice? inputDevice;
    private WaveFileWriter? writer;
    private WasapiOut? monitorOutput;
    private MMDevice? monitorOutputDevice;
    private string? inputDeviceId;
    private string? outputDeviceId;
    // IDs of the devices actually opened, cached so callers never query a device that has just disappeared.
    private string? openInputDeviceId;
    private string? openMonitorOutputDeviceId;
    private BufferedWaveProvider? monitorBuffer;
    private bool isRecording;
    private bool monitorEnabled;
    private long recordedBytes;
    private string? recordingFilePath;

    public event EventHandler<RecordingProgressEventArgs>? RecordingProgress;

    /// <summary>Raised when capture or monitoring output stops because of an error, such as a lost device.</summary>
    public event EventHandler<AudioDeviceFailedEventArgs>? DeviceFailed;

    public bool IsRecording => isRecording;
    public bool IsMonitoring => monitorEnabled;

    /// <summary>The ID of the microphone currently open, or null when capture is stopped.</summary>
    public string? ActiveInputDeviceId => input is null ? null : openInputDeviceId;

    /// <summary>The ID of the output device used for monitoring, or null when monitoring is off.</summary>
    public string? ActiveMonitorOutputDeviceId => monitorOutput is null ? null : openMonitorOutputDeviceId;

    /// <summary>
    /// Selects the microphone and monitoring output. Null means the system default.
    /// Not allowed while recording. If monitoring is on, it restarts on the new devices;
    /// if that restart fails, monitoring is left off and the exception is rethrown.
    /// </summary>
    public void SetDevices(string? newInputDeviceId, string? newOutputDeviceId)
    {
        if (isRecording)
        {
            throw new InvalidOperationException("Stop recording before changing audio devices.");
        }

        var restartMonitoring = monitorEnabled;
        if (restartMonitoring)
        {
            DisableMonitoring();
            StopCapture();
        }

        inputDeviceId = newInputDeviceId;
        outputDeviceId = newOutputDeviceId;
        if (restartMonitoring)
        {
            EnableMonitoring();
        }
    }
    public bool HasCapturedAudio
    {
        get
        {
            lock (captureLock)
            {
                return recordedBytes > 0;
            }
        }
    }

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
        FileStream? recordingStream = null;
        WaveFileWriter? newWriter = null;

        try
        {
            recordingStream = new FileStream(outputPath, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
            newWriter = new WaveFileWriter(recordingStream, activeInput.WaveFormat);
            recordingStream = null;
            lock (captureLock)
            {
                writer = newWriter;
                recordingFilePath = outputPath;
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
            newWriter?.Dispose();
            recordingStream?.Dispose();
            lock (captureLock)
            {
                if (ReferenceEquals(writer, newWriter))
                {
                    writer = null;
                    recordingFilePath = null;
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
                DisposeCaptureDevice();
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
            recordingFilePath = null;
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

    public void WriteRecoverySnapshot(string destinationPath)
    {
        lock (captureLock)
        {
            if (writer is null || recordingFilePath is null || recordedBytes == 0)
            {
                throw new InvalidOperationException("There is no captured audio available to recover yet.");
            }

            writer.Flush();
            CopyRecoverablePcmSnapshot(recordingFilePath, destinationPath, recordedBytes);
        }
    }

    public static void CopyRecoverablePcmSnapshot(string sourcePath, string destinationPath, long recordedBytes)
    {
        if (recordedBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(recordedBytes));
        }

        using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var (dataSizePosition, dataOffset) = FindWaveDataChunk(source);
        var snapshotLength = dataOffset + recordedBytes;
        if (source.Length < snapshotLength)
        {
            throw new InvalidDataException("The interrupted recording file is shorter than its recorded audio.");
        }

        using var destination = new FileStream(destinationPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        source.Position = 0;
        source.CopyTo(destination);
        destination.SetLength(snapshotLength);

        // WaveFileWriter finalizes these RIFF sizes on disposal; patch the copy so the active take remains untouched.
        using var binaryWriter = new BinaryWriter(destination, System.Text.Encoding.UTF8, leaveOpen: true);
        binaryWriter.BaseStream.Position = 4;
        binaryWriter.Write(checked((uint)(snapshotLength - 8)));
        binaryWriter.BaseStream.Position = dataSizePosition;
        binaryWriter.Write(checked((uint)recordedBytes));
        binaryWriter.Flush();
    }

    private static (long DataSizePosition, long DataOffset) FindWaveDataChunk(Stream stream)
    {
        using var reader = new BinaryReader(stream, System.Text.Encoding.ASCII, leaveOpen: true);
        stream.Position = 0;
        var riffId = System.Text.Encoding.ASCII.GetString(reader.ReadBytes(4));
        var riffSize = reader.ReadUInt32();
        var waveId = System.Text.Encoding.ASCII.GetString(reader.ReadBytes(4));
        if (riffId != "RIFF" || waveId != "WAVE" || riffSize > stream.Length - 8)
        {
            throw new InvalidDataException("The interrupted recording has an invalid WAV header.");
        }

        while (stream.Position + 8 <= stream.Length)
        {
            var chunkId = System.Text.Encoding.ASCII.GetString(reader.ReadBytes(4));
            var chunkSizePosition = stream.Position;
            var chunkSize = reader.ReadUInt32();
            var chunkDataPosition = stream.Position;
            if (chunkId == "data")
            {
                return (chunkSizePosition, chunkDataPosition);
            }

            var nextChunkPosition = chunkDataPosition + chunkSize + (chunkSize & 1);
            if (nextChunkPosition > stream.Length)
            {
                break;
            }

            stream.Position = nextChunkPosition;
        }

        throw new InvalidDataException("The interrupted recording has no WAV data chunk.");
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

            if (monitorEnabled && monitorBuffer is { } buffer)
            {
                if (buffer.BufferedBytes + e.BytesRecorded > buffer.BufferLength)
                {
                    buffer.ClearBuffer();
                }

                buffer.AddSamples(e.Buffer, 0, e.BytesRecorded);
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

    private WasapiCapture CreateInput()
    {
        var device = AudioDeviceCatalog.OpenDevice(DataFlow.Capture, inputDeviceId);
        try
        {
            var capture = new WasapiCapture(device, useEventSync: true, audioBufferMillisecondsLength: CaptureBufferMilliseconds)
            {
                WaveFormat = new WaveFormat(44100, 16, 1)
            };
            capture.DataAvailable += OnDataAvailable;
            capture.RecordingStopped += OnCaptureStopped;
            inputDevice = device;
            openInputDeviceId = device.ID;
            return capture;
        }
        catch
        {
            device.Dispose();
            throw;
        }
    }

    private void OnCaptureStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception is not null && ReferenceEquals(sender, input))
        {
            DeviceFailed?.Invoke(this, new AudioDeviceFailedEventArgs(openInputDeviceId, e.Exception));
        }
    }

    private void OnMonitorOutputStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception is not null && ReferenceEquals(sender, monitorOutput))
        {
            DeviceFailed?.Invoke(this, new AudioDeviceFailedEventArgs(openMonitorOutputDeviceId, e.Exception));
        }
    }

    private void EnableMonitoring()
    {
        var activeInput = input;
        var newInput = activeInput is null ? CreateInput() : null;
        activeInput ??= newInput;
        var buffer = new BufferedWaveProvider(activeInput!.WaveFormat)
        {
            BufferDuration = TimeSpan.FromMilliseconds(MonitorBufferMilliseconds),
            DiscardOnBufferOverflow = true
        };
        WasapiOut? output = null;
        MMDevice? outputDevice = null;

        try
        {
            outputDevice = AudioDeviceCatalog.OpenDevice(DataFlow.Render, outputDeviceId);
            output = new WasapiOut(
                outputDevice,
                AudioClientShareMode.Shared,
                useEventSync: true,
                latency: MonitorLatencyMilliseconds);
            output.PlaybackStopped += OnMonitorOutputStopped;
            output.Init(buffer);
            output.Play();
            lock (captureLock)
            {
                monitorBuffer = buffer;
                monitorOutput = output;
                monitorOutputDevice = outputDevice;
                openMonitorOutputDeviceId = outputDevice.ID;
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
                monitorOutputDevice = null;
                if (ReferenceEquals(input, newInput))
                {
                    input = null;
                }
            }

            if (output is not null)
            {
                output.PlaybackStopped -= OnMonitorOutputStopped;
                output.Dispose();
            }

            outputDevice?.Dispose();
            if (newInput is not null)
            {
                newInput.Dispose();
                DisposeCaptureDevice();
            }

            throw;
        }
    }

    private void DisableMonitoring()
    {
        WasapiOut? output;
        MMDevice? outputDevice;
        lock (captureLock)
        {
            monitorEnabled = false;
            monitorBuffer = null;
            output = monitorOutput;
            monitorOutput = null;
            outputDevice = monitorOutputDevice;
            monitorOutputDevice = null;
            openMonitorOutputDeviceId = null;
        }

        if (output is not null)
        {
            output.PlaybackStopped -= OnMonitorOutputStopped;
        }

        try
        {
            output?.Stop();
        }
        finally
        {
            output?.Dispose();
            outputDevice?.Dispose();
        }
    }

    private void StopCapture(WasapiCapture? expectedInput = null)
    {
        WasapiCapture? activeInput;
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
            activeInput.RecordingStopped -= OnCaptureStopped;
            activeInput.Dispose();
            DisposeCaptureDevice();
        }
    }

    private void DisposeCaptureDevice()
    {
        inputDevice?.Dispose();
        inputDevice = null;
        openInputDeviceId = null;
    }

    public void Dispose()
    {
        Stop();
        DisableMonitoring();
        StopCapture();
    }
}
