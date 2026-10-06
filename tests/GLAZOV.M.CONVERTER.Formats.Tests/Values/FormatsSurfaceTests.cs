namespace GLAZOV.M.CONVERTER.Formats.Tests.Values;

/// <summary>
/// The public surface of the Formats package: the value classes, the written form, the shared detectors and the extension; nothing else
/// (the marker types the compiler makes for the extension members are left out).
/// </summary>
public sealed class FormatsSurfaceTests
{
    [Fact]
    public void ExportedTypes_AreExactlyTheValueClassesTheMembersTheDetectorsAndTheExtension()
    {
        string[] exported = [.. typeof(FormatsExtension).Assembly.GetExportedTypes().Select(type => type.Name).Where(name => !name.StartsWith('<'))];

        Assert.Equal(
            ["AudioFormats", "DocumentFormats", "FileFormatMembers", "FormatsExtension", "ImageFormats", "StandardDetectors", "VideoFormats"],
            exported.Order());
    }

    [Fact]
    public void StandardDetectors_ExposeTheSixSharedInstances()
    {
        string[] names = [.. typeof(StandardDetectors).GetProperties().Select(property => property.Name)];

        Assert.Equal(["Audio", "Documents", "Image", "Svg", "Text", "Video"], names.Order());
    }
}
