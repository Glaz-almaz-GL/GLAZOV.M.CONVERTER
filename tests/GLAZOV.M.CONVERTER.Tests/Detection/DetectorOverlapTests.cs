using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Detection;
using GLAZOV.M.CONVERTER.Extensions;

namespace GLAZOV.M.CONVERTER.Tests.Detection;

/// <summary>
/// The family detectors do not overlap: a sample is named by one of them at most, so the order of asking decides nothing.
/// </summary>
public sealed class DetectorOverlapTests
{
    public static IEnumerable<object[]> Samples()
    {
        string folder = Path.Combine(AppContext.BaseDirectory, "Samples");
        return Directory.EnumerateFiles(folder).Order(StringComparer.Ordinal).Select(path => new object[] { Path.GetFileName(path) });
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void EverySample_IsNamedByAtMostOneFamilyDetector(string name)
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Samples", name));
        byte[] head = bytes.AsSpan(0, Math.Min(bytes.Length, 64 * 1024)).ToArray();

        FileFormat?[] named = [.. BuiltInDetectors.Create().Select(detector => detector.Detect(head)).Where(format => format is not null)];

        Assert.True(named.Length <= 1, $"{name}: named by {named.Length} detectors: {string.Join(", ", named.Select(f => f!.Name))}");
    }
}
