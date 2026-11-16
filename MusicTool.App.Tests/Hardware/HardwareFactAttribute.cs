using Xunit;

namespace MusicTool.App.Tests.Hardware;

/// <summary>
/// A test that uses this PC's real audio devices. It is skipped unless the environment
/// variable <c>MUSICTOOL_HARDWARE_TESTS</c> is <c>1</c>, so a normal <c>dotnet test</c>
/// passes on any machine. Tests using it also carry <c>[Trait("Category", "Hardware")]</c>.
/// </summary>
public sealed class HardwareFactAttribute : FactAttribute
{
    public const string EnableVariable = "MUSICTOOL_HARDWARE_TESTS";

    public HardwareFactAttribute()
    {
        if (!IsEnabled)
        {
            Skip = $"Hardware test: set {EnableVariable}=1 to run it on real audio devices.";
        }
    }

    public static bool IsEnabled => Environment.GetEnvironmentVariable(EnableVariable) == "1";
}
