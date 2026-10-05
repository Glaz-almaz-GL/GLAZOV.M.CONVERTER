using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Settings;
using GLAZOV.M.CONVERTER.Tests.Detection;
using GLAZOV.M.CONVERTER.Tests.Extension;

namespace GLAZOV.M.CONVERTER.Tests.Converting;

/// <summary>
/// Public type detection of the converter: the same detectors and window as before a conversion.
/// </summary>
public sealed class ConverterDetectAsyncTests
{
    private static Converter WithDemo()
    {
        ConverterSettings settings = new();
        settings.Extensions.Add(new DemoExtension());
        return new Converter(settings);
    }

    [Fact]
    public async Task DetectAsync_GivesWhatTheConversionReports_AndPutsTheStreamBack()
    {
        Converter converter = WithDemo();
        byte[] bytes = [.. new byte[3], .. "DEMO данные"u8.ToArray()];
        using MemoryStream stream = new(bytes) { Position = 3 };

        FileFormat detected = await converter.DetectAsync(Source.Stream(stream));
        Assert.Equal(3, stream.Position);
        ConversionResult result = await converter.ConvertAsync(Source.Stream(stream), Target.Memory(FileFormat.Wav));

        Assert.Equal(DemoExtension.Demo, detected);
        Assert.Equal(result.SourceType, detected);
    }

    [Fact]
    public async Task DetectAsync_WithoutTheExtension_DoesNotKnowTheFormat()
    {
        using MemoryStream stream = new("DEMO данные"u8.ToArray());

        Assert.Equal(FileFormat.Text, await new Converter().DetectAsync(Source.Stream(stream)));
    }

    [Fact]
    public async Task DetectAsync_StreamThatCannotSeek_IsRejected()
    {
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await WithDemo().DetectAsync(Source.Stream(new ForwardReadStream("DEMO"u8.ToArray()))));
    }

    [Fact]
    public async Task DetectAsync_Folder_IsRejected()
    {
        await Assert.ThrowsAsync<ArgumentException>(async () => await WithDemo().DetectAsync(Source.Folder(Path.GetTempPath())));
    }
}
