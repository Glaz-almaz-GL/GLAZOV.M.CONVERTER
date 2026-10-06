using GLAZOV.M.CONVERTER.Capabilities;
using GLAZOV.M.CONVERTER.Extensions;
using GLAZOV.M.CONVERTER.Ffmpeg.Engines;
using GLAZOV.M.CONVERTER.Settings;
using GLAZOV.M.CONVERTER.Tooling;

namespace GLAZOV.M.CONVERTER.Ffmpeg.Tests.Tools;

/// <summary>
/// The description of the FFmpeg programs: the usual places on every system and a real run.
/// </summary>
public sealed class FfmpegToolsTests
{
    [Fact]
    public void Description_KnowsTheUsualPlacesOfTheProgram()
    {
        Assert.Contains(FfmpegTools.Ffmpeg.SearchPlaces[ToolSystem.Windows], place => place.Contains("WinGet", StringComparison.Ordinal));
        Assert.Contains(FfmpegTools.Ffmpeg.SearchPlaces[ToolSystem.MacOS], place => place == "/opt/homebrew/bin");
        Assert.Contains(FfmpegTools.Ffmpeg.SearchPlaces[ToolSystem.Linux], place => place == "/usr/bin");
    }

    [Fact]
    public void Ffprobe_IsOptional_AndFoundBesideFfmpeg()
    {
        Assert.False(FfmpegTools.Ffprobe.Required);
        Assert.Equal("ffmpeg", FfmpegTools.Ffprobe.FoundBeside);
        Assert.True(FfmpegTools.Ffmpeg.Required);
    }

    [ExternalFact]
    public async Task RealFfmpeg_IsFound_AndRuns()
    {
        ToolLocator locator = new(ConverterSettingsSnapshot.Create(new ConverterSettings()));

        ToolStatus status = locator.Locate(FfmpegTools.Ffmpeg);
        ToolResult result = await ToolRunner.RunAsync(new ToolInvocation(status.Path!, ["-version"]));

        Assert.True(status.Found);
        Assert.True(File.Exists(status.Path));
        Assert.Equal(0, result.ExitCode);
    }
}
