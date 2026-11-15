using MusicTool.App.Services;
using Xunit;

namespace MusicTool.App.Tests;

public sealed class AudioDeviceCatalogTests
{
    private static readonly AudioDeviceInfo[] ConnectedDevices =
    [
        new("{0.0.1.00000000}.{snowball}", "Blue Snowball"),
        new("{0.0.1.00000000}.{headset-mic}", "HyperX Headset Microphone")
    ];

    [Fact]
    public void SavedDeviceThatIsConnectedIsUsed()
    {
        var resolution = AudioDeviceCatalog.Resolve("{0.0.1.00000000}.{snowball}", ConnectedDevices);

        Assert.Equal("{0.0.1.00000000}.{snowball}", resolution.DeviceId);
        Assert.False(resolution.FellBack);
    }

    [Fact]
    public void SavedDeviceIdMatchIgnoresCase()
    {
        var resolution = AudioDeviceCatalog.Resolve("{0.0.1.00000000}.{SNOWBALL}", ConnectedDevices);

        Assert.Equal("{0.0.1.00000000}.{SNOWBALL}", resolution.DeviceId);
        Assert.False(resolution.FellBack);
    }

    [Fact]
    public void SavedDeviceThatIsMissingFallsBackToSystemDefault()
    {
        var resolution = AudioDeviceCatalog.Resolve("{0.0.1.00000000}.{unplugged}", ConnectedDevices);

        Assert.Null(resolution.DeviceId);
        Assert.True(resolution.FellBack);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void NoSavedDeviceUsesSystemDefaultWithoutFallback(string? savedId)
    {
        var resolution = AudioDeviceCatalog.Resolve(savedId, ConnectedDevices);

        Assert.Null(resolution.DeviceId);
        Assert.False(resolution.FellBack);
    }

    [Fact]
    public void SavedDeviceWithNoConnectedDevicesFallsBack()
    {
        var resolution = AudioDeviceCatalog.Resolve("{0.0.1.00000000}.{snowball}", []);

        Assert.Null(resolution.DeviceId);
        Assert.True(resolution.FellBack);
    }
}
