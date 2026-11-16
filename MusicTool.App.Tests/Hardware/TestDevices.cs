using MusicTool.App.Services;
using NAudio.CoreAudioApi;

namespace MusicTool.App.Tests.Hardware;

/// <summary>The real devices the hardware tests use. <see cref="OtherOutput"/> is null when not connected.</summary>
public sealed record TestDeviceSet(AudioDeviceInfo Mic, AudioDeviceInfo Output, AudioDeviceInfo? OtherOutput);

/// <summary>
/// Chooses test devices by a case-insensitive part of their Windows name. Defaults match the
/// owner's devices and can be overridden with environment variables (see MT-012).
/// </summary>
public static class TestDevices
{
    public const string MicVariable = "MUSICTOOL_TEST_MIC";
    public const string OutputVariable = "MUSICTOOL_TEST_OUTPUT";
    public const string OtherOutputVariable = "MUSICTOOL_TEST_OTHER_OUTPUT";

    public static TestDeviceSet Resolve()
    {
        var mics = AudioDeviceCatalog.ListActive(DataFlow.Capture);
        var outputs = AudioDeviceCatalog.ListActive(DataFlow.Render);

        var mic = FindRequired("microphone", MicVariable, ReadPattern(MicVariable, "Snowball"), mics);
        var output = FindRequired("output", OutputVariable, ReadPattern(OutputVariable, "HyperX"), outputs);

        // Any device matching the "other" pattern will do; it must not be the main output.
        var otherPattern = ReadPattern(OtherOutputVariable, "NVIDIA");
        var other = outputs.FirstOrDefault(device =>
            Matches(device, otherPattern)
            && !string.Equals(device.Id, output.Id, StringComparison.OrdinalIgnoreCase));

        return new TestDeviceSet(mic, output, other);
    }

    /// <summary>Message used when a required device is missing or the name matches several devices.</summary>
    public static string DescribeProblem(
        string kind, string variable, string pattern, IReadOnlyList<AudioDeviceInfo> connected, IReadOnlyList<AudioDeviceInfo> matches)
    {
        var problem = matches.Count == 0
            ? $"No connected {kind} has a name containing '{pattern}'."
            : $"{matches.Count} connected {kind}s have a name containing '{pattern}': {Names(matches)}. Use a more specific name.";
        return $"{problem} Set {variable} to part of the device name. Connected {kind}s: {Names(connected)}.";
    }

    private static AudioDeviceInfo FindRequired(
        string kind, string variable, string pattern, IReadOnlyList<AudioDeviceInfo> connected)
    {
        var matches = connected.Where(device => Matches(device, pattern)).ToList();
        if (matches.Count != 1)
        {
            throw new InvalidOperationException(DescribeProblem(kind, variable, pattern, connected, matches));
        }

        return matches[0];
    }

    private static bool Matches(AudioDeviceInfo device, string pattern) =>
        device.Name.Contains(pattern, StringComparison.OrdinalIgnoreCase);

    private static string ReadPattern(string variable, string fallback)
    {
        var value = Environment.GetEnvironmentVariable(variable);
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static string Names(IEnumerable<AudioDeviceInfo> devices)
    {
        var names = devices.Select(device => $"'{device.Name}'").ToList();
        return names.Count == 0 ? "(none)" : string.Join(", ", names);
    }
}
