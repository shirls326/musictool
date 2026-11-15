using MusicTool.App.Models;
using MusicTool.App.Services;
using Xunit;

namespace MusicTool.App.Tests;

public sealed class AppSettingsStoreTests
{
    [Fact]
    public void SavedSettingsRoundTrip()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var store = new AppSettingsStore(root);
            store.Save(new AppSettings
            {
                InputDeviceId = "{0.0.1.00000000}.{mic-id}",
                InputDeviceName = "Blue Snowball",
                OutputDeviceId = "{0.0.0.00000000}.{headset-id}",
                OutputDeviceName = "HyperX Headset"
            });

            var loaded = new AppSettingsStore(root).Load();

            Assert.Equal("{0.0.1.00000000}.{mic-id}", loaded.InputDeviceId);
            Assert.Equal("Blue Snowball", loaded.InputDeviceName);
            Assert.Equal("{0.0.0.00000000}.{headset-id}", loaded.OutputDeviceId);
            Assert.Equal("HyperX Headset", loaded.OutputDeviceName);
            Assert.Equal(Path.Combine(root, "settings.json"), store.SettingsFilePath);
            Assert.False(File.Exists(store.SettingsFilePath + ".tmp"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SystemDefaultChoicesRoundTripAsNull()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var store = new AppSettingsStore(root);
            store.Save(new AppSettings { InputDeviceId = "mic", InputDeviceName = "Mic" });
            store.Save(new AppSettings());

            var loaded = store.Load();

            Assert.Null(loaded.InputDeviceId);
            Assert.Null(loaded.InputDeviceName);
            Assert.Null(loaded.OutputDeviceId);
            Assert.Null(loaded.OutputDeviceName);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void MissingSettingsFileLoadsDefaults()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var loaded = new AppSettingsStore(Path.Combine(root, "does-not-exist")).Load();

            Assert.Null(loaded.InputDeviceId);
            Assert.Null(loaded.OutputDeviceId);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("{ this is not json")]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("[1, 2, 3]")]
    public void CorruptSettingsFileLoadsDefaults(string contents)
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var store = new AppSettingsStore(root);
            File.WriteAllText(store.SettingsFilePath, contents);

            var loaded = store.Load();

            Assert.Null(loaded.InputDeviceId);
            Assert.Null(loaded.OutputDeviceId);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "MusicToolTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
