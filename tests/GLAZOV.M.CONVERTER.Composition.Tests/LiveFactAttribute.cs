using GLAZOV.M.CONVERTER.Capabilities;

namespace GLAZOV.M.CONVERTER.Composition.Tests;

/// <summary>
/// A check that needs real programs. If one of them is not on the machine the check is skipped with the reason,
/// so that a skipped check is told from a passed one.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class LiveFactAttribute : FactAttribute
{
    public LiveFactAttribute(params string[] tools)
    {
        ConverterCapabilities caps = AllPackages.Converter("Formats,Ffmpeg,Office,Svg,Pdf").Capabilities;
        string[] missing = [.. tools.Where(tool => !caps.Tool(tool).Found)];
        if (missing.Length > 0)
        {
            Skip = $"Not found on this machine: {string.Join(", ", missing)}. The live check was not run (neither passed nor failed).";
        }
    }
}
