using System.Diagnostics;
using System.Globalization;
using GLAZOV.M.CONVERTER.Capabilities;
using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Detection;
using GLAZOV.M.CONVERTER.Engines;
using GLAZOV.M.CONVERTER.Extensions;
using GLAZOV.M.CONVERTER.Settings;
using GLAZOV.M.CONVERTER.Tooling;

namespace GLAZOV.M.CONVERTER.Tests.Engines;

/// <summary>
/// Copying tracks without encoding, on real files: the decision by codecs, per track, the Reencode flag,
/// a missing ffprobe, a container that refuses a copied track, and every row of the codec table by a real copy.
/// </summary>
[Collection("FFmpeg")]
public sealed class FfmpegVideoCopyTests : IClassFixture<VideoSourcesFixture>, IDisposable
{
    private static readonly Dictionary<string, string> VideoEncoders = new()
    {
        ["h264"] = "libx264",
        ["hevc"] = "libx265",
        ["mpeg4"] = "mpeg4",
        ["av1"] = "libsvtav1",
        ["vp9"] = "libvpx-vp9",
        ["vp8"] = "libvpx",
        ["mjpeg"] = "mjpeg",
    };

    private static readonly Dictionary<string, string> AudioEncoders = new()
    {
        ["aac"] = "aac",
        ["mp3"] = "libmp3lame",
        ["ac3"] = "ac3",
        ["opus"] = "libopus",
        ["vorbis"] = "libvorbis",
        ["pcm_s16le"] = "pcm_s16le",
    };

    // A codec of each kind that every container of the row copies, so a row test changes only the track under test.
    private static readonly Dictionary<string, (string Video, string Audio)> Companions = new()
    {
        ["MP4"] = ("h264", "aac"),
        ["MOV"] = ("h264", "aac"),
        ["WebM"] = ("vp9", "opus"),
        ["AVI"] = ("mpeg4", "mp3"),
    };

    private readonly VideoSourcesFixture _fixture;
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "convtests-" + Guid.NewGuid().ToString("N"));
    private readonly FfmpegVideoEngine _engine = new();

    public FfmpegVideoCopyTests(VideoSourcesFixture fixture)
    {
        _fixture = fixture;
        Directory.CreateDirectory(_temp);
    }

    public void Dispose()
    {
        Directory.Delete(_temp, recursive: true);
    }

    public static IEnumerable<object[]> TableRows()
    {
        foreach (FileFormat container in new[] { FileFormat.Mp4, FileFormat.Mov, FileFormat.Webm, FileFormat.Avi })
        {
            (IReadOnlyList<string> video, IReadOnlyList<string> audio) = ContainerCodecs.RowsOf(container);
            foreach (string codec in video)
            {
                yield return new object[] { container.Name, "video", codec };
            }

            foreach (string codec in audio)
            {
                yield return new object[] { container.Name, "audio", codec };
            }
        }
    }

    private static FileFormat FormatOf(string name)
    {
        return new[] { FileFormat.Mp4, FileFormat.Mov, FileFormat.Webm, FileFormat.Avi, FileFormat.Mkv }.Single(f => f.Name == name);
    }

    private ToolStatus Probe()
    {
        return new ToolStatus("ffprobe", true, _fixture.Ffprobe, string.Empty);
    }

    private static ToolStatus NoProbe()
    {
        return new ToolStatus("ffprobe", false, null, "no ffprobe");
    }

    private static ToolStatus Ffmpeg()
    {
        return new ToolLocator(ConverterSettingsSnapshot.Create(new ConverterSettings())).Locate("ffmpeg");
    }

    private async Task<(ConversionRequest Request, string ResultPath)> ConvertAsync(
        string sourcePath,
        FileFormat to,
        ConversionOptions? options = null,
        ToolStatus? probe = null,
        ToolStatus? tool = null)
    {
        await using FileStream input = File.OpenRead(sourcePath);
        using MemoryStream output = new();
        FileFormat from = SignatureMatcher.Match(await File.ReadAllBytesAsync(sourcePath));
        ConversionRequest request = new(
            input, output, from, to, options ?? new ConversionOptions(), tool ?? Ffmpeg(), _temp, sourcePath, null, probe);

        await _engine.ConvertAsync(request, CancellationToken.None);

        string resultPath = Path.Combine(_temp, $"result-{Guid.NewGuid():N}.{to.Extension}");
        await File.WriteAllBytesAsync(resultPath, output.ToArray());
        return (request, resultPath);
    }

    private Task<string> Codec(string path, string stream)
    {
        return _fixture.ProbeAsync(path, "stream=codec_name", stream);
    }

    private async Task<double> BitRate(string path, string stream)
    {
        return double.Parse(await _fixture.ProbeAsync(path, "stream=bit_rate", stream), CultureInfo.InvariantCulture);
    }

    private async Task MakeSourceAsync(string path, string? videoCodec, string? audioCodec)
    {
        List<string> args = ["-v", "error", "-y"];
        if (videoCodec is not null)
        {
            args.AddRange(["-f", "lavfi", "-i", "testsrc=size=64x64:rate=10:duration=1"]);
        }

        if (audioCodec is not null)
        {
            args.AddRange(["-f", "lavfi", "-i", "sine=frequency=440:duration=1"]);
        }

        if (videoCodec is not null)
        {
            args.AddRange(["-c:v", VideoEncoders[videoCodec], "-pix_fmt", "yuv420p"]);
            if (videoCodec == "vp9")
            {
                args.AddRange(["-deadline", "realtime", "-cpu-used", "8"]);
            }
        }

        if (audioCodec is not null)
        {
            args.AddRange(["-c:a", AudioEncoders[audioCodec]]);
        }

        args.Add(path);
        ToolResult result = await ToolRunner.RunAsync(new ToolInvocation(_fixture.Ffmpeg, args));
        Assert.Equal(0, result.ExitCode);
    }

    [ExternalFact]
    public async Task H264AndAac_FromMkvToMp4_AreCopied_QuicklyAndWithoutLoss()
    {
        string source = _fixture.Ten("mkv");

        Stopwatch copyWatch = Stopwatch.StartNew();
        (ConversionRequest copied, string copyPath) = await ConvertAsync(source, FileFormat.Mp4, probe: Probe());
        copyWatch.Stop();
        Stopwatch encodeWatch = Stopwatch.StartNew();
        (ConversionRequest encoded, string encodePath) = await ConvertAsync(
            source, FileFormat.Mp4, new ConversionOptions { Video = new VideoOptions { Reencode = true } }, Probe());
        encodeWatch.Stop();

        Assert.False(copied.Reencoded);
        Assert.True(encoded.Reencoded);
        Assert.Equal("h264", await Codec(copyPath, "v:0"));
        Assert.Equal("aac", await Codec(copyPath, "a:0"));
        double sourceSeconds = double.Parse(await _fixture.ProbeAsync(source, "format=duration"), CultureInfo.InvariantCulture);
        double copySeconds = double.Parse(await _fixture.ProbeAsync(copyPath, "format=duration"), CultureInfo.InvariantCulture);
        Assert.InRange(copySeconds, sourceSeconds - 0.1, sourceSeconds + 0.1);
        double sizeRatio = new FileInfo(copyPath).Length / (double)new FileInfo(source).Length;
        Assert.InRange(sizeRatio, 0.95, 1.05);
        Assert.True(copyWatch.Elapsed * 2 <= encodeWatch.Elapsed, $"copy {copyWatch.Elapsed}, encode {encodeWatch.Elapsed}");
        Assert.Equal("aac", await Codec(encodePath, "a:0"));
    }

    [ExternalFact]
    public async Task Vp9AndOpus_FromWebmToMp4_AreEncoded_BecauseOpusIsNotInTheMp4Row()
    {
        (ConversionRequest request, string path) = await ConvertAsync(_fixture.Rich("webm"), FileFormat.Mp4, probe: Probe());

        Assert.True(request.Reencoded);
        Assert.Equal("aac", await Codec(path, "a:0"));
        // VP9 fits MP4 and is copied; only the audio had to be encoded.
        Assert.Equal("vp9", await Codec(path, "v:0"));
    }

    [ExternalFact]
    public async Task ReencodeFlag_EncodesEverything_EvenWhenCodecsFit()
    {
        ConversionOptions options = new() { Video = new VideoOptions { Reencode = true } };

        (ConversionRequest request, string path) = await ConvertAsync(_fixture.Ten("mp4"), FileFormat.Mp4, options, Probe());

        Assert.True(request.Reencoded);
        Assert.Equal("h264", await Codec(path, "v:0"));
        // The source audio is 64 kbit/s; encoding gives the engine default of about 128.
        Assert.True(await BitRate(path, "a:0") > 100_000);
    }

    [ExternalFact]
    public async Task VideoSettings_EncodeTheVideo_AndTheAudioIsStillCopied()
    {
        ConversionOptions options = new() { Video = new VideoOptions { Quality = 50 } };

        (ConversionRequest request, string path) = await ConvertAsync(_fixture.Ten("mp4"), FileFormat.Mp4, options, Probe());

        Assert.True(request.Reencoded);
        Assert.InRange(await BitRate(path, "a:0"), 55_000, 75_000);
    }

    [ExternalFact]
    public async Task AudioOptions_EncodeTheAudio_AndTheVideoIsStillCopied()
    {
        ConversionOptions options = new() { Audio = new AudioOptions { Bitrate = 128_000 } };
        string source = _fixture.Ten("mp4");

        (ConversionRequest request, string path) = await ConvertAsync(source, FileFormat.Mp4, options, Probe());

        Assert.True(request.Reencoded);
        Assert.True(await BitRate(path, "a:0") > 100_000);
        // A copied video stream keeps its bit rate to the unit; an encoded one would not.
        Assert.Equal(await BitRate(source, "v:0"), await BitRate(path, "v:0"));
    }

    [ExternalFact]
    public async Task ConvertPixelFormat_EncodesA444Video_ToCommonYuv420p_AndOtherwiseItIsCopiedAsIs()
    {
        string source = Path.Combine(_temp, "yuv444.mkv");
        ToolResult made = await ToolRunner.RunAsync(new ToolInvocation(
            _fixture.Ffmpeg,
            ["-v", "error", "-y", "-f", "lavfi", "-i", "testsrc=size=64x64:rate=10:duration=1", "-c:v", "libx264", "-pix_fmt", "yuv444p", source]));
        Assert.Equal(0, made.ExitCode);

        (ConversionRequest kept, string keptPath) = await ConvertAsync(source, FileFormat.Mp4, probe: Probe());
        ConversionOptions options = new() { Video = new VideoOptions { ConvertPixelFormat = true } };
        (ConversionRequest converted, string convertedPath) = await ConvertAsync(source, FileFormat.Mp4, options, Probe());

        Assert.False(kept.Reencoded);
        Assert.Equal("yuv444p", await _fixture.ProbeAsync(keptPath, "stream=pix_fmt", "v:0"));
        Assert.True(converted.Reencoded);
        Assert.Equal("yuv420p", await _fixture.ProbeAsync(convertedPath, "stream=pix_fmt", "v:0"));
    }

    [ExternalFact]
    public async Task ConvertPixelFormat_LeavesACommonFormatCopied()
    {
        ConversionOptions options = new() { Video = new VideoOptions { ConvertPixelFormat = true } };

        (ConversionRequest request, _) = await ConvertAsync(_fixture.Ten("mkv"), FileFormat.Mp4, options, Probe());

        Assert.False(request.Reencoded);
    }

    [ExternalFact]
    public async Task WithoutFfprobe_EverythingIsEncoded_WithoutAnError()
    {
        (ConversionRequest request, string path) = await ConvertAsync(_fixture.Ten("mkv"), FileFormat.Mp4, probe: NoProbe());

        Assert.True(request.Reencoded);
        Assert.Equal("h264", await Codec(path, "v:0"));
        Assert.True(await BitRate(path, "a:0") > 100_000);
    }

    [ExternalFact]
    public async Task WithoutAProbeInTheRequest_EverythingIsEncoded()
    {
        (ConversionRequest request, _) = await ConvertAsync(_fixture.Ten("mkv"), FileFormat.Mp4);

        Assert.True(request.Reencoded);
    }

    [ExternalFact]
    public async Task ThroughTheConverter_Default_CopiesAndReportsNotReencoded()
    {
        ConversionResult result = await new Converter().ConvertAsync(Source.File(_fixture.Ten("mkv")), Target.Memory(FileFormat.Mp4));

        Assert.True(result.Succeeded);
        Assert.False(result.Reencoded);
    }

    [ExternalFact]
    public async Task ThroughTheConverter_StreamInput_StillCopies()
    {
        await using FileStream input = File.OpenRead(_fixture.Ten("mkv"));

        ConversionResult result = await new Converter().ConvertAsync(Source.Stream(input), Target.Memory(FileFormat.Mp4));

        Assert.False(result.Reencoded);
        Assert.Empty(Directory.GetFiles(Path.GetTempPath(), "input-*.tmp"));
    }

    [ExternalFact]
    public async Task ThroughTheConverter_FfprobePathToNowhere_EncodesWithoutAnError()
    {
        ConverterSettings settings = new();
        settings.ToolPaths["ffprobe"] = Path.Combine(_temp, "no-such-folder", "ffprobe.exe");

        ConversionResult result = await new Converter(settings).ConvertAsync(Source.File(_fixture.Ten("mkv")), Target.Memory(FileFormat.Mp4));

        Assert.True(result.Succeeded);
        Assert.True(result.Reencoded);
    }

    [ExternalFact]
    public async Task WhenTheContainerRefusesACopiedTrack_TheTrackIsEncodedInstead()
    {
        string real = Ffmpeg().Path!;
        string wrapper = Path.Combine(_temp, OperatingSystem.IsWindows() ? "refusing-ffmpeg.cmd" : "refusing-ffmpeg.sh");
        await File.WriteAllTextAsync(wrapper, OperatingSystem.IsWindows()
            ? $"@echo off\r\necho %* | findstr /C:\"copy\" >nul\r\nif not errorlevel 1 (\r\n  echo refused to copy 1>&2\r\n  exit /b 1\r\n)\r\n\"{real}\" %*\r\n"
            : $"#!/bin/sh\ncase \"$*\" in *copy*) echo 'refused to copy' >&2; exit 1;; esac\nexec \"{real}\" \"$@\"\n");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(wrapper, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        ToolStatus refusing = new("ffmpeg", true, wrapper, string.Empty);

        (ConversionRequest request, string path) = await ConvertAsync(_fixture.Ten("mkv"), FileFormat.Mp4, probe: Probe(), tool: refusing);

        Assert.True(request.Reencoded);
        Assert.Equal("h264", await Codec(path, "v:0"));
        Assert.True(await BitRate(path, "a:0") > 100_000);
    }

    [ExternalTheory]
    [MemberData(nameof(TableRows))]
    public async Task EveryRowOfTheTable_IsCopiedForReal_ByTheInstalledFfmpeg(string container, string kind, string codec)
    {
        FileFormat to = FormatOf(container);
        (string companionVideo, string companionAudio) = Companions[container];
        string videoCodec = kind == "video" ? codec : companionVideo;
        string audioCodec = kind == "audio" ? codec : companionAudio;
        string source = Path.Combine(_temp, "row-source.mkv");
        await MakeSourceAsync(source, videoCodec, audioCodec);

        (ConversionRequest request, string path) = await ConvertAsync(source, to, probe: Probe());

        Assert.False(request.Reencoded, $"{container} did not take {kind} {codec} as it is");
        Assert.Equal(videoCodec, await Codec(path, "v:0"));
        Assert.Equal(audioCodec, await Codec(path, "a:0"));
    }

    [ExternalTheory]
    [InlineData("h264")]
    [InlineData("vp9")]
    [InlineData("mpeg4")]
    public async Task Matroska_TakesAnyVideoCodec_AsItIs(string videoCodec)
    {
        string source = Path.Combine(_temp, "any-source.mp4");
        await MakeSourceAsync(source, videoCodec, "aac");

        (ConversionRequest request, string path) = await ConvertAsync(source, FileFormat.Mkv, probe: Probe());

        Assert.False(request.Reencoded);
        Assert.Equal(videoCodec, await Codec(path, "v:0"));
    }

    [ExternalTheory]
    [InlineData("MP4")]
    [InlineData("MOV")]
    public async Task HevcCopiedFromMatroska_IsTaggedHvc1_SoApplePlayersTakeIt(string container)
    {
        string source = Path.Combine(_temp, "hevc-source.mkv");
        await MakeSourceAsync(source, "hevc", "aac");

        (ConversionRequest request, string path) = await ConvertAsync(source, FormatOf(container), probe: Probe());

        Assert.False(request.Reencoded);
        Assert.Equal("hevc", await Codec(path, "v:0"));
        Assert.Equal("hvc1", await _fixture.ProbeAsync(path, "stream=codec_tag_string", "v:0"));
    }
}
