using GLAZOV.M.CONVERTER.Formats;

namespace GLAZOV.M.CONVERTER.Ffmpeg.Tests.Capabilities;

/// <summary>
/// The extension brings the detectors of the formats its engines read: images, video and audio, in this order.
/// </summary>
public sealed class FfmpegExtensionDetectorsTests
{
    [Fact]
    public void Extension_NamesTheImageVideoAndAudioDetectors()
    {
        Assert.Equal([StandardDetectors.Image, StandardDetectors.Video, StandardDetectors.Audio], new FfmpegExtension().Detectors);
    }
}
