using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Extensions;
using GLAZOV.M.CONVERTER.Settings;
using GLAZOV.M.CONVERTER.Tests.Detection;
using GLAZOV.M.CONVERTER.Tests.Extension;

namespace GLAZOV.M.CONVERTER.Tests.Converting;

/// <summary>
/// Public type detection of the converter: the same detectors and window as before a conversion.
/// </summary>
public sealed class ConverterDetectAsyncTests
{
    private static Converter WithStandIns()
    {
        ConverterSettings settings = new();
        settings.Extensions.Add(TestExtensions.Create(new LambdaEngine(null, [new ConversionPair(TestFormats.Alpha, TestFormats.Beta)])));
        return new Converter(settings);
    }

    [Fact]
    public async Task DetectAsync_GivesWhatTheConversionReports_AndPutsTheStreamBack()
    {
        Converter converter = WithStandIns();
        byte[] bytes = [.. new byte[3], .. TestFormats.Sample(TestFormats.Alpha)];
        using MemoryStream stream = new(bytes) { Position = 3 };

        FileFormat detected = await converter.DetectAsync(Source.Stream(stream));
        Assert.Equal(3, stream.Position);
        ConversionResult result = await converter.ConvertAsync(Source.Stream(stream), Target.Memory(TestFormats.Beta));

        Assert.Equal(TestFormats.Alpha, detected);
        Assert.Equal(result.SourceType, detected);
    }

    [Fact]
    public async Task DetectAsync_WithoutTheExtension_DoesNotKnowTheFormat()
    {
        using MemoryStream stream = new(TestFormats.Sample(TestFormats.Alpha));

        Assert.NotEqual(TestFormats.Alpha, await new Converter().DetectAsync(Source.Stream(stream)));
    }

    [Fact]
    public async Task DetectAsync_StreamThatCannotSeek_IsRejected()
    {
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await WithStandIns().DetectAsync(Source.Stream(new ForwardReadStream(TestFormats.Header(TestFormats.Alpha)))));
    }

    [Fact]
    public async Task DetectAsync_Folder_IsRejected()
    {
        await Assert.ThrowsAsync<ArgumentException>(async () => await WithStandIns().DetectAsync(Source.Folder(Path.GetTempPath())));
    }
}
