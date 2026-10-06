using System.Text;
using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Extensions;
using GLAZOV.M.CONVERTER.Settings;
using GLAZOV.M.CONVERTER.Tests.Extension;

namespace GLAZOV.M.CONVERTER.Tests.Capabilities;

/// <summary>
/// Formats declared by the connected extensions: the same value in two of them works together, two different values with one MIME type are an error.
/// </summary>
public sealed class FormatDeclarationsTests
{
    private static Converter With(params IConverterExtension[] extensions)
    {
        ConverterSettings settings = new();
        foreach (IConverterExtension extension in extensions)
        {
            settings.Extensions.Add(extension);
        }

        return new Converter(settings);
    }

    [Fact]
    public async Task TwoExtensionsWithTheSameJson_WorkTogether_TheEarlierDetectorAndEngineWin()
    {
        FileFormat first = FileFormat.Create("JSON", "application/json", "json");
        FileFormat second = FileFormat.Create("json", "Application/JSON", "json");
        LambdaEngine engineA = new(null, [new ConversionPair(first, TestFormats.Beta)], marker: "A:"u8.ToArray());
        LambdaEngine engineB = new(null, [new ConversionPair(second, TestFormats.Beta)], marker: "B:"u8.ToArray());
        Converter converter = With(
            new ConfigurableExtension("первое", [new PatternDetector("{"u8.ToArray(), first)], [engineA]),
            new ConfigurableExtension("второе", [new PatternDetector("{"u8.ToArray(), second)], [engineB]));
        string path = Path.Combine(Path.GetTempPath(), "convtests-" + Guid.NewGuid().ToString("N") + ".json");
        await File.WriteAllBytesAsync(path, Encoding.UTF8.GetBytes("{\"a\":1}"));

        try
        {
            ConversionResult result = await converter.ConvertAsync(Source.File(path), Target.Memory(TestFormats.Beta));

            Assert.Equal(first, result.SourceType);
            Assert.StartsWith("A:", Encoding.UTF8.GetString(result.Bytes!));
            Assert.Equal(1, engineA.Calls);
            Assert.Equal(0, engineB.Calls);
            Assert.Single(converter.Capabilities.Formats, f => f.Extension == "json");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void TwoDifferentValuesWithOneMimeType_AreRejected_WithBothExtensionsAndBothDescriptions()
    {
        FileFormat json = FileFormat.Create("JSON", "application/json", "json");
        FileFormat other = FileFormat.Create("Json5", "Application/Json", "json5");

        ArgumentException ex = Assert.Throws<ArgumentException>(() => With(
            new ConfigurableExtension("первый", [], []) { Declared = [json] },
            new ConfigurableExtension("второй", [], []) { Declared = [other] }));

        Assert.Contains("первый", ex.Message);
        Assert.Contains("второй", ex.Message);
        Assert.Contains("JSON", ex.Message);
        Assert.Contains("json5", ex.Message);
        Assert.Contains("application/json", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Formats_ListEveryDeclaredFormatOnce_InTheOrderOfConnection()
    {
        FileFormat one = FileFormat.Create("ONE", "application/x-one", "one");
        FileFormat two = FileFormat.Create("TWO", "application/x-two", "two");

        IReadOnlyList<FileFormat> formats = With(
            new ConfigurableExtension("a", [], []) { Declared = [one, two] },
            new ConfigurableExtension("b", [], []) { Declared = [two] }).Capabilities.Formats;

        Assert.Equal(formats.Distinct().Count(), formats.Count);
        Assert.True(formats.ToList().IndexOf(one) < formats.ToList().IndexOf(two));
        Assert.Contains(one, formats);
        Assert.Contains(two, formats);
        Assert.True(formats.Count > 2, "the built-in formats are listed too");
    }

    [Fact]
    public void ByDefault_AnExtensionDeclaresTheFormatsOfItsPairs()
    {
        FileFormat only = FileFormat.Create("ONLY", "application/x-only", "only");
        ConfigurableExtension extension = new("пары", [], [new LambdaEngine(null, [new ConversionPair(only, TestFormats.Beta)])]);

        Assert.Equal([only, TestFormats.Beta], extension.Formats);
        Assert.Contains(only, With(extension).Capabilities.Formats);
    }
}
