using System.Windows;
using System.Windows.Controls;
using MusicTool.App.Models;
using MusicTool.App.Services;

namespace MusicTool.App;

/// <summary>
/// Modal window for choosing the microphone and the shared playback/monitoring output.
/// It only edits a copy of the settings; MainWindow applies and saves the result.
/// </summary>
public partial class SettingsWindow : Window
{
    private const string SystemDefaultLabel = "System default";

    public SettingsWindow(
        AppSettings current,
        IReadOnlyList<AudioDeviceInfo> inputDevices,
        IReadOnlyList<AudioDeviceInfo> outputDevices,
        string inputInUse,
        string outputInUse,
        bool isMonitoring)
    {
        InitializeComponent();
        Populate(InputComboBox, inputDevices, current.InputDeviceId, current.InputDeviceName);
        Populate(OutputComboBox, outputDevices, current.OutputDeviceId, current.OutputDeviceName);
        InputInUseText.Text = $"Currently using: {inputInUse}";
        OutputInUseText.Text = $"Currently using: {outputInUse}";
        MonitoringStatusText.Text = isMonitoring ? "Input monitoring: on" : "Input monitoring: off";
    }

    /// <summary>The chosen settings after OK, or null when the window was cancelled.</summary>
    public AppSettings? Result { get; private set; }

    private static void Populate(
        ComboBox comboBox,
        IReadOnlyList<AudioDeviceInfo> devices,
        string? savedId,
        string? savedName)
    {
        var choices = new List<DeviceChoice> { new(null, null, SystemDefaultLabel) };
        choices.AddRange(devices.Select(device => new DeviceChoice(device.Id, device.Name, device.Name)));

        // Keep a saved-but-unplugged device selectable, so pressing OK without changes
        // does not silently replace the saved preference with System default.
        if (savedId is not null && !devices.Any(device => string.Equals(device.Id, savedId, StringComparison.OrdinalIgnoreCase)))
        {
            var name = string.IsNullOrWhiteSpace(savedName) ? "Saved device" : savedName;
            choices.Add(new DeviceChoice(savedId, savedName, $"{name} (not connected)"));
        }

        comboBox.ItemsSource = choices;
        comboBox.SelectedItem = choices.FirstOrDefault(choice =>
            string.Equals(choice.Id, savedId, StringComparison.OrdinalIgnoreCase)) ?? choices[0];
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        var input = (DeviceChoice)InputComboBox.SelectedItem;
        var output = (DeviceChoice)OutputComboBox.SelectedItem;
        Result = new AppSettings
        {
            InputDeviceId = input.Id,
            InputDeviceName = input.Name,
            OutputDeviceId = output.Id,
            OutputDeviceName = output.Name
        };
        DialogResult = true;
    }

    private sealed record DeviceChoice(string? Id, string? Name, string Label)
    {
        // The ComboBox displays items with ToString().
        public override string ToString() => Label;
    }
}
