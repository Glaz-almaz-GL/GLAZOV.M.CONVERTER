using System.Drawing;
using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Ffmpeg.Engines;

namespace GLAZOV.M.CONVERTER.Ffmpeg.Tests.Settings;

/// <summary>
/// Settings of the FFmpeg extension: defaults give the former arguments, a changed value changes them,
/// a wrong value is rejected and an explicit call option wins. No FFmpeg is started.
/// </summary>
public class FfmpegSettingsTests
{
    private static string ValueAfter(IReadOnlyList<string> args, string name)
    {
        int index = args.ToList().IndexOf(name);
        Assert.True(index >= 0 && index + 1 < args.Count, $"No argument {name}");
        return args[index + 1];
    }

    private static IReadOnlyList<string> Video(FileFormat to, FfmpegSettings? settings, VideoOptions? video = null, AudioOptions? audio = null)
    {
        return FfmpegVideoArguments.Build(to, video, audio, "in.bin", "out.bin", settings: settings);
    }

    [Fact]
    public void Defaults_EqualTheFormerConstants()
    {
        FfmpegSettings s = new();

        Assert.Equal((2, 5, 5, 128_000), (s.Mp3Quality, s.OggQuality, s.FlacCompression, s.AacBitrate));
        Assert.Equal((90, 80, Color.White), (s.JpegQuality, s.WebpQuality, s.FlattenBackground));
        Assert.Equal((23, 31, 5), (s.H264Crf, s.Vp9Crf, s.Mpeg4Qscale));
        Assert.Equal(("good", 4), (s.Vp9Deadline, s.Vp9CpuUsed));
    }

    [Fact]
    public void DefaultSettings_GiveTheSameArgumentsAsNoSettings()
    {
        FfmpegSettings s = new();

        Assert.Equal(FfmpegAudioArguments.Build(FileFormat.Mp3, null, "x"), FfmpegAudioArguments.Build(FileFormat.Mp3, null, "x", settings: s));
        Assert.Equal(FfmpegAudioArguments.Build(FileFormat.Flac, null, "x"), FfmpegAudioArguments.Build(FileFormat.Flac, null, "x", settings: s));
        Assert.Equal(FfmpegImageArguments.Build(FileFormat.Jpeg, null, "i", "o"), FfmpegImageArguments.Build(FileFormat.Jpeg, null, "i", "o", settings: s));
        Assert.Equal(Video(FileFormat.Webm, null), Video(FileFormat.Webm, s));
    }

    [Fact]
    public void DefaultVp9Arguments_KeepTheFormerSpeed()
    {
        IReadOnlyList<string> args = Video(FileFormat.Webm, null);

        Assert.Equal("good", ValueAfter(args, "-deadline"));
        Assert.Equal("4", ValueAfter(args, "-cpu-used"));
        Assert.Equal("1", ValueAfter(args, "-row-mt"));
    }

    [Theory]
    [InlineData("Mp3", 7, "-q:a", "7")]
    [InlineData("Ogg", 8, "-q:a", "8")]
    [InlineData("Flac", 9, "-compression_level", "9")]
    public void AudioQualitySettings_ChangeTheArguments(string format, int value, string argument, string expected)
    {
        FfmpegSettings s = new() { Mp3Quality = format == "Mp3" ? value : 2, OggQuality = format == "Ogg" ? value : 5, FlacCompression = format == "Flac" ? value : 5 };
        FileFormat to = format switch { "Mp3" => FileFormat.Mp3, "Ogg" => FileFormat.Ogg, _ => FileFormat.Flac };

        Assert.Equal(expected, ValueAfter(FfmpegAudioArguments.Build(to, null, "x", settings: s), argument));
    }

    [Fact]
    public void AacBitrate_ChangesM4a_AndTheSoundOfMp4()
    {
        FfmpegSettings s = new() { AacBitrate = 192_000 };

        Assert.Equal("192000", ValueAfter(FfmpegAudioArguments.Build(FileFormat.M4a, null, "x", settings: s), "-b:a"));
        Assert.Equal("192000", ValueAfter(Video(FileFormat.Mp4, s), "-b:a"));
    }

    [Fact]
    public void Mp3Quality_AlsoChangesTheSoundOfAvi()
    {
        Assert.Equal("6", ValueAfter(Video(FileFormat.Avi, new FfmpegSettings { Mp3Quality = 6 }), "-q:a"));
    }

    [Fact]
    public void ImageSettings_ChangeQualityAndBackground()
    {
        FfmpegSettings s = new() { JpegQuality = 50, WebpQuality = 30, FlattenBackground = Color.FromArgb(255, 16, 32, 255) };

        IReadOnlyList<string> jpeg = FfmpegImageArguments.Build(FileFormat.Jpeg, null, "i", "o", settings: s);
        IReadOnlyList<string> webp = FfmpegImageArguments.Build(FileFormat.Webp, null, "i", "o", settings: s);

        Assert.Equal("17", ValueAfter(jpeg, "-q:v"));
        Assert.Contains("drawbox=c=0x1020FF@1", ValueAfter(jpeg, "-vf"));
        Assert.Equal("30", ValueAfter(webp, "-quality"));
    }

    [Fact]
    public void VideoSettings_ChangeTheScaleDefaults()
    {
        FfmpegSettings s = new() { H264Crf = 18, Vp9Crf = 40, Mpeg4Qscale = 8, Vp9Deadline = "realtime", Vp9CpuUsed = 7 };

        Assert.Equal("18", ValueAfter(Video(FileFormat.Mp4, s), "-crf"));
        IReadOnlyList<string> webm = Video(FileFormat.Webm, s);
        Assert.Equal("40", ValueAfter(webm, "-crf"));
        Assert.Equal("realtime", ValueAfter(webm, "-deadline"));
        Assert.Equal("7", ValueAfter(webm, "-cpu-used"));
        Assert.Equal("8", ValueAfter(Video(FileFormat.Avi, s), "-q:v"));
    }

    [Fact]
    public void ExplicitCallOptions_BeatTheSettings()
    {
        FfmpegSettings s = new() { Mp3Quality = 7, JpegQuality = 50, H264Crf = 18, AacBitrate = 192_000 };

        Assert.Equal("128000", ValueAfter(FfmpegAudioArguments.Build(FileFormat.Mp3, new AudioOptions { Bitrate = 128_000 }, "x", settings: s), "-b:a"));
        Assert.Equal("2", ValueAfter(FfmpegImageArguments.Build(FileFormat.Jpeg, 100, "i", "o", settings: s), "-q:v"));
        Assert.Equal("12", ValueAfter(Video(FileFormat.Mp4, s, new VideoOptions { Quality = 100 }), "-crf"));
        Assert.Equal("96000", ValueAfter(Video(FileFormat.Mp4, s, audio: new AudioOptions { Bitrate = 96_000 }), "-b:a"));
    }

    [Theory]
    [InlineData(nameof(FfmpegSettings.Mp3Quality), -1)]
    [InlineData(nameof(FfmpegSettings.Mp3Quality), 10)]
    [InlineData(nameof(FfmpegSettings.OggQuality), 11)]
    [InlineData(nameof(FfmpegSettings.FlacCompression), 13)]
    [InlineData(nameof(FfmpegSettings.AacBitrate), 0)]
    [InlineData(nameof(FfmpegSettings.JpegQuality), 0)]
    [InlineData(nameof(FfmpegSettings.WebpQuality), 101)]
    [InlineData(nameof(FfmpegSettings.H264Crf), 52)]
    [InlineData(nameof(FfmpegSettings.Vp9Crf), 64)]
    [InlineData(nameof(FfmpegSettings.Mpeg4Qscale), 0)]
    [InlineData(nameof(FfmpegSettings.Vp9CpuUsed), 9)]
    public void ValueOutOfRange_IsRejected_AndTheOldValueStays(string property, int value)
    {
        FfmpegSettings s = new();
        System.Reflection.PropertyInfo info = typeof(FfmpegSettings).GetProperty(property)!;
        object before = info.GetValue(s)!;

        TargetInvocationExceptionAssert(() => info.SetValue(s, value));

        Assert.Equal(before, info.GetValue(s));
    }

    [Fact]
    public void UnknownVp9Deadline_IsRejected()
    {
        FfmpegSettings s = new();

        Assert.Throws<ArgumentOutOfRangeException>(() => s.Vp9Deadline = "fast");
        Assert.Equal("good", s.Vp9Deadline);
    }

    [Fact]
    public void Extension_TakesACopy_SoLaterChangesDoNotReachIt()
    {
        FfmpegSettings s = new() { JpegQuality = 50 };
        FfmpegExtension extension = new(s);
        s.JpegQuality = 99;

        Assert.Equal(3, extension.Engines.Count);
        Assert.Throws<ArgumentNullException>(() => new FfmpegExtension(null!));
    }

    [Fact]
    public void Extension_PassesItsCopyToEveryEngine_NotTheCallersObject()
    {
        FfmpegSettings s = new() { JpegQuality = 50, H264Crf = 20, Mp3Quality = 4 };
        FfmpegExtension extension = new(s);
        s.JpegQuality = 99;
        s.H264Crf = 40;
        s.Mp3Quality = 9;

        List<FfmpegSettings> seen = [];
        foreach (object engine in extension.Engines)
        {
            System.Reflection.FieldInfo field = engine.GetType().GetField("_settings", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            Assert.NotNull(field);
            FfmpegSettings held = (FfmpegSettings)field.GetValue(engine)!;
            Assert.NotSame(s, held);
            seen.Add(held);
        }

        Assert.All(seen, held => Assert.Equal((50, 20, 4), (held.JpegQuality, held.H264Crf, held.Mp3Quality)));
    }

    private static void TargetInvocationExceptionAssert(Action action)
    {
        System.Reflection.TargetInvocationException ex = Assert.Throws<System.Reflection.TargetInvocationException>(action);
        Assert.IsType<ArgumentOutOfRangeException>(ex.InnerException);
    }
}
