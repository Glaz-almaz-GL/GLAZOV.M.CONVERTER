using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Engines;
using GLAZOV.M.CONVERTER.Ffmpeg.Engines;
using GLAZOV.M.CONVERTER.Ffmpeg.Progress;
using GLAZOV.M.CONVERTER.Formats;

namespace GLAZOV.M.CONVERTER.Ffmpeg.Tests.Engines;

/// <summary>
/// The decision "copy the track or encode it" and the reading of ffprobe's answer, without any program.
/// </summary>
public class CopyPlanTests
{
    private static readonly TrackCodecs H264Aac = new("h264", "aac");

    private static CopyPlan Decide(FileFormat to, VideoOptions? video = null, AudioOptions? audio = null, bool operations = false, TrackCodecs? codecs = null)
    {
        return CopyPlan.Decide(to, video, audio, operations, codecs ?? H264Aac);
    }

    [Fact]
    public void NothingAsked_AndCodecsFit_CopiesBothTracks()
    {
        CopyPlan plan = Decide(FileFormat.Mp4);

        Assert.Equal(new CopyPlan(true, true), plan);
        Assert.True(plan.CopiesEverything(H264Aac));
    }

    [Fact]
    public void UnreadableTracks_MeanEncoding()
    {
        Assert.Equal(CopyPlan.EncodeAll, CopyPlan.Decide(FileFormat.Mp4, null, null, false, null));
        Assert.False(CopyPlan.EncodeAll.CopiesEverything(null));
    }

    [Fact]
    public void ReencodeFlag_EncodesBothTracks()
    {
        Assert.Equal(CopyPlan.EncodeAll, Decide(FileFormat.Mp4, new VideoOptions { Reencode = true }));
    }

    [Theory]
    [InlineData("quality")]
    [InlineData("bitrate")]
    [InlineData("width")]
    [InlineData("height")]
    [InlineData("rate")]
    [InlineData("operations")]
    public void VideoChange_EncodesTheVideoOnly_AndKeepsTheAudioCopy(string change)
    {
        VideoOptions video = new();
        switch (change)
        {
            case "quality": video.Quality = 50; break;
            case "bitrate": video.Bitrate = 1_000_000; break;
            case "width": video.Width = 320; break;
            case "height": video.Height = 240; break;
            case "rate": video.FrameRate = 10; break;
        }

        CopyPlan plan = Decide(FileFormat.Mp4, video, operations: change == "operations");

        Assert.Equal(new CopyPlan(false, true), plan);
        Assert.False(plan.CopiesEverything(H264Aac));
    }

    [Theory]
    [InlineData("bitrate")]
    [InlineData("rate")]
    [InlineData("channels")]
    public void AudioChange_EncodesTheAudioOnly_AndKeepsTheVideoCopy(string change)
    {
        AudioOptions audio = new();
        switch (change)
        {
            case "bitrate": audio.Bitrate = 96_000; break;
            case "rate": audio.SampleRate = 22050; break;
            case "channels": audio.Channels = 1; break;
        }

        Assert.Equal(new CopyPlan(true, false), Decide(FileFormat.Mp4, audio: audio));
    }

    [Fact]
    public void EmptyOptionObjects_AskForNothing()
    {
        Assert.Equal(new CopyPlan(true, true), Decide(FileFormat.Mp4, new VideoOptions(), new AudioOptions()));
    }

    [Theory]
    [InlineData("MP4", "vp9", "opus", true, false)]
    [InlineData("MP4", "h264", "opus", true, false)]
    [InlineData("MOV", "vp9", "aac", false, true)]
    [InlineData("WebM", "h264", "aac", false, false)]
    [InlineData("WebM", "vp9", "opus", true, true)]
    [InlineData("AVI", "h264", "mp3", false, true)]
    [InlineData("AVI", "mpeg4", "aac", true, false)]
    [InlineData("MKV", "anything", "whatever", true, true)]
    public void CodecsAreCheckedPerTrack_AgainstTheTableOfTheContainer(string container, string video, string audio, bool copyVideo, bool copyAudio)
    {
        FileFormat to = new[] { FileFormat.Mp4, FileFormat.Mov, FileFormat.Webm, FileFormat.Avi, FileFormat.Mkv }.Single(f => f.Name == container);

        // MP4 with vp9 and Opus: the video fits, the audio does not (Opus is not in the MP4 row).
        Assert.Equal(new CopyPlan(copyVideo, copyAudio), Decide(to, codecs: new TrackCodecs(video, audio)));
    }

    [Theory]
    [InlineData("yuv444p", false, true)]
    [InlineData("yuv420p10le", false, true)]
    [InlineData("yuv444p", true, false)]
    [InlineData("yuv420p10le", true, false)]
    [InlineData("yuv420p", true, true)]
    [InlineData("yuvj420p", true, true)]
    [InlineData(null, true, true)]
    public void ConvertPixelFormat_EncodesOnlyAnUnusualFormat(string? pixelFormat, bool convert, bool copyVideo)
    {
        TrackCodecs codecs = new("h264", "aac", pixelFormat);

        CopyPlan plan = Decide(FileFormat.Mp4, new VideoOptions { ConvertPixelFormat = convert }, codecs: codecs);

        Assert.Equal(new CopyPlan(copyVideo, true), plan);
    }

    [Fact]
    public void Parse_ReadsThePixelFormatOfTheFirstVideo()
    {
        const string json = """{"streams":[{"codec_name":"h264","codec_type":"video","pix_fmt":"yuv444p"},{"codec_name":"aac","codec_type":"audio"}]}""";

        Assert.Equal(new TrackCodecs("h264", "aac", "yuv444p"), TrackProbe.Parse(json));
    }

    [Fact]
    public void InputWithoutSound_CountsAsCopiedWhenTheVideoIs()
    {
        TrackCodecs silent = new("h264", null);
        CopyPlan plan = Decide(FileFormat.Mp4, codecs: silent);

        Assert.True(plan.CopiesEverything(silent));
    }

    [Fact]
    public void Parse_TakesTheFirstVideoAndTheFirstAudio()
    {
        const string json = """
            {"streams":[
              {"codec_name":"h264","codec_type":"video"},
              {"codec_name":"aac","codec_type":"audio"},
              {"codec_name":"mp3","codec_type":"audio"},
              {"codec_name":"mjpeg","codec_type":"video"},
              {"codec_name":"subrip","codec_type":"subtitle"}]}
            """;

        Assert.Equal(new TrackCodecs("h264", "aac"), TrackProbe.Parse(json));
    }

    [Fact]
    public void Parse_HandlesMissingTracksAndUnreadableText()
    {
        Assert.Equal(new TrackCodecs(null, "opus"), TrackProbe.Parse("""{"streams":[{"codec_name":"opus","codec_type":"audio"}]}"""));
        Assert.Equal(new TrackCodecs(null, null), TrackProbe.Parse("""{"streams":[]}"""));
        Assert.Equal(new TrackCodecs("unknown", null), TrackProbe.Parse("""{"streams":[{"codec_type":"video"}]}"""));
        Assert.Null(TrackProbe.Parse(string.Empty));
        Assert.Null(TrackProbe.Parse("not json"));
        Assert.Null(TrackProbe.Parse("{}"));
        Assert.Null(TrackProbe.Parse("""{"streams":5}"""));
        Assert.Equal(new TrackCodecs(null, null), TrackProbe.Parse("""{"streams":[1,"x",null]}"""));
    }
}
