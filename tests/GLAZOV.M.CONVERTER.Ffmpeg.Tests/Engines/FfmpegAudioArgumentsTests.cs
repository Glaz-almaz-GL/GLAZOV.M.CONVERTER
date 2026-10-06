using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Engines;
using GLAZOV.M.CONVERTER.Ffmpeg.Engines;
using GLAZOV.M.CONVERTER.Ffmpeg.Progress;
using GLAZOV.M.CONVERTER.Formats;

namespace GLAZOV.M.CONVERTER.Ffmpeg.Tests.Engines;

/// <summary>
/// Аргументы FFmpeg для звука — чистые проверки, FFmpeg не нужен: кодек и умолчания по формату,
/// параметры только когда заданы, пути не требуют экранирования.
/// </summary>
public class FfmpegAudioArgumentsTests
{
    private static string Joined(IReadOnlyList<string> args)
    {
        return string.Join(' ', args);
    }

    private static string ValueAfter(IReadOnlyList<string> args, string name)
    {
        int index = args.ToList().IndexOf(name);
        Assert.True(index >= 0 && index + 1 < args.Count, $"Нет аргумента {name}");
        return args[index + 1];
    }

    [Fact]
    public void Always_ReadsStdin_DropsPictures_AndWritesToTheGivenTarget()
    {
        IReadOnlyList<string> args = FfmpegAudioArguments.Build(FileFormat.Mp3, null, "pipe:1");

        Assert.Equal("pipe:0", ValueAfter(args, "-i"));
        Assert.Contains("-vn", args);
        Assert.Equal("pipe:1", args[^1]);
        Assert.Equal("error", ValueAfter(args, "-loglevel"));
    }

    [Theory]
    [InlineData("MP3", "libmp3lame", "mp3")]
    [InlineData("OGG", "libvorbis", "ogg")]
    [InlineData("WAV", "pcm_s16le", "wav")]
    public void CodecAndContainer_FollowTheTargetFormat(string name, string codec, string container)
    {
        FileFormat to = name switch
        {
            "MP3" => FileFormat.Mp3,
            "OGG" => FileFormat.Ogg,
            _ => FileFormat.Wav,
        };

        IReadOnlyList<string> args = FfmpegAudioArguments.Build(to, null, "x");

        Assert.Equal(codec, ValueAfter(args, "-c:a"));
        Assert.Equal(container, ValueAfter(args, "-f"));
    }

    [Fact]
    public void Defaults_Mp3AndOgg_UseVariableQuality_WavUsesNone()
    {
        Assert.Equal("2", ValueAfter(FfmpegAudioArguments.Build(FileFormat.Mp3, null, "x"), "-q:a"));
        Assert.Equal("5", ValueAfter(FfmpegAudioArguments.Build(FileFormat.Ogg, null, "x"), "-q:a"));

        IReadOnlyList<string> wav = FfmpegAudioArguments.Build(FileFormat.Wav, null, "x");
        Assert.DoesNotContain("-q:a", wav);
        Assert.DoesNotContain("-b:a", wav);
    }

    [Fact]
    public void NothingSet_NoOptionalArgumentsAppear()
    {
        IReadOnlyList<string> args = FfmpegAudioArguments.Build(FileFormat.Mp3, new AudioOptions(), "x");

        Assert.DoesNotContain("-b:a", args);
        Assert.DoesNotContain("-ar", args);
        Assert.DoesNotContain("-ac", args);
    }

    [Fact]
    public void AllSet_AppearAsGiven_AndBitrateReplacesDefaultQuality()
    {
        AudioOptions audio = new() { Bitrate = 192_000, SampleRate = 44_100, Channels = 2 };

        IReadOnlyList<string> args = FfmpegAudioArguments.Build(FileFormat.Mp3, audio, "x");

        Assert.Equal("192000", ValueAfter(args, "-b:a"));
        Assert.Equal("44100", ValueAfter(args, "-ar"));
        Assert.Equal("2", ValueAfter(args, "-ac"));
        Assert.DoesNotContain("-q:a", args);
    }

    [Fact]
    public void OnlyTheSetParameters_Appear()
    {
        IReadOnlyList<string> args = FfmpegAudioArguments.Build(FileFormat.Ogg, new AudioOptions { Channels = 1 }, "x");

        Assert.Equal("1", ValueAfter(args, "-ac"));
        Assert.DoesNotContain("-ar", args);
        Assert.DoesNotContain("-b:a", args);
        Assert.Equal("5", ValueAfter(args, "-q:a"));
    }

    [Fact]
    public void Wav_IgnoresBitrate_BecauseThereIsNoCompression()
    {
        IReadOnlyList<string> args = FfmpegAudioArguments.Build(FileFormat.Wav, new AudioOptions { Bitrate = 128_000 }, "x");

        Assert.DoesNotContain("-b:a", args);
    }

    [Fact]
    public void PathWithSpacesAndQuotes_IsOneArgument_NotEscaped()
    {
        const string path = "C:\\Мои файлы\\a \"b\" c.wav";

        IReadOnlyList<string> args = FfmpegAudioArguments.Build(FileFormat.Wav, null, path);

        Assert.Equal(path, args[^1]);
        Assert.DoesNotContain(path.Replace("\"", "\\\""), args);
        Assert.Single(args, a => a == path);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void UnsupportedTarget_IsRefused(int which)
    {
        FileFormat to = which == 0 ? FileFormat.Jpeg : FileFormat.Unknown;

        Assert.Throws<ArgumentException>(() => FfmpegAudioArguments.Build(to, null, "x"));
    }

    [Fact]
    public void EmptyTarget_IsAUsageError()
    {
        Assert.Throws<ArgumentException>(() => FfmpegAudioArguments.Build(FileFormat.Mp3, null, " "));
        Assert.Contains("pipe:1", Joined(FfmpegAudioArguments.Build(FileFormat.Mp3, null, "pipe:1")));
    }

    [Fact]
    public void Flac_UsesTheFlacCodecAndCompressionLevel_IgnoresBitrate()
    {
        IReadOnlyList<string> args = FfmpegAudioArguments.Build(FileFormat.Flac, new AudioOptions { Bitrate = 128_000, SampleRate = 48_000 }, "x");

        Assert.Equal("flac", ValueAfter(args, "-c:a"));
        Assert.Equal("5", ValueAfter(args, "-compression_level"));
        Assert.Equal("flac", ValueAfter(args, "-f"));
        Assert.Equal("48000", ValueAfter(args, "-ar"));
        Assert.DoesNotContain("-b:a", args);
        Assert.DoesNotContain("-q:a", args);
    }

    [Fact]
    public void M4a_UsesAacWithDefaultBitrate_IpodContainer_AndFaststart()
    {
        IReadOnlyList<string> args = FfmpegAudioArguments.Build(FileFormat.M4a, null, "x");

        Assert.Equal("aac", ValueAfter(args, "-c:a"));
        Assert.Equal("128000", ValueAfter(args, "-b:a"));
        Assert.Equal("ipod", ValueAfter(args, "-f"));
        Assert.Equal("+faststart", ValueAfter(args, "-movflags"));
        Assert.DoesNotContain("-q:a", args);
    }

    [Fact]
    public void M4a_BitrateFromSettings_ReplacesTheDefault()
    {
        IReadOnlyList<string> args = FfmpegAudioArguments.Build(FileFormat.M4a, new AudioOptions { Bitrate = 192_000 }, "x");

        Assert.Equal("192000", ValueAfter(args, "-b:a"));
        Assert.Single(args, a => a == "-b:a");
    }
}
