using System.Diagnostics;
using GLAZOV.M.CONVERTER.Batch;
using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Detection;
using GLAZOV.M.CONVERTER.Errors;
using GLAZOV.M.CONVERTER.Settings;

namespace GLAZOV.M.CONVERTER.Tests.Live;

/// <summary>
/// Run of video on real files through the public calls and the real FFmpeg: all 25 video pairs and all 25 video-to-sound
/// pairs, stream without rewinding, memory, a folder in parallel, cancellation, broken and lying inputs, progress,
/// copy against encode on every pair, the settings. Results are read back by the real ffprobe. When the variable
/// CONVERTER_RUN_KEEP names a folder, the results are kept there for the owner to watch and listen to.
/// Not checked on Linux and macOS: everything here ran on Windows.
/// </summary>
[Collection("FFmpeg")]
public sealed class VideoRunTests(VideoRunFixture fixture) : IClassFixture<VideoRunFixture>, IDisposable
{
    private static readonly (FileFormat Format, string Name, string Video, string Audio)[] VideoTargets =
    [
        (FileFormat.Mp4, "mp4", "h264", "aac"),
        (FileFormat.Mov, "mov", "h264", "aac"),
        (FileFormat.Mkv, "mkv", "h264", "aac"),
        (FileFormat.Webm, "webm", "vp9", "opus"),
        (FileFormat.Avi, "avi", "mpeg4", "mp3"),
    ];

    private static readonly (FileFormat Format, string Name, string Codec)[] SoundTargets =
    [
        (FileFormat.Mp3, "mp3", "mp3"),
        (FileFormat.Wav, "wav", "pcm_s16le"),
        (FileFormat.Ogg, "ogg", "vorbis"),
        (FileFormat.Flac, "flac", "flac"),
        (FileFormat.M4a, "m4a", "aac"),
    ];

    // Whether the pair is expected to be re-encoded, by container: "source -> targets that need encoding".
    // H.264 and AAC go as they are into MP4, MOV and MKV; MPEG-4 and MP3 also into those; VP9 fits MP4 and MKV
    // but Opus does not fit MP4, and MOV takes neither; WebM takes only VP8, VP9, AV1 with Opus or Vorbis; AVI takes MPEG-4.
    private static readonly Dictionary<string, string[]> Encoded = new()
    {
        ["mp4"] = ["webm", "avi"],
        ["mov"] = ["webm", "avi"],
        ["mkv"] = ["webm", "avi"],
        ["webm"] = ["mp4", "mov", "avi"],
        ["avi"] = ["webm"],
    };

    private readonly string _work = Path.Combine(Path.GetTempPath(), "convrun-video-out-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_work))
        {
            Directory.Delete(_work, recursive: true);
        }
    }

    private string Out(string name)
    {
        Directory.CreateDirectory(_work);
        return Path.Combine(_work, name);
    }

    private static void Keep(string produced, string label)
    {
        string? keep = Environment.GetEnvironmentVariable("CONVERTER_RUN_KEEP");
        if (string.IsNullOrWhiteSpace(keep))
        {
            return;
        }

        string folder = Path.Combine(keep, "video");
        Directory.CreateDirectory(folder);
        File.Copy(produced, Path.Combine(folder, label + Path.GetExtension(produced)), overwrite: true);
    }

    [ExternalFact]
    public async Task EveryVideoPair_File_ToFile_ReadsBack_AndCopyOrEncodeIsAsExpected()
    {
        Converter converter = new();
        int done = 0;

        foreach ((string container, string path, string video, string audio) in fixture.Inputs)
        {
            foreach ((FileFormat to, string name, string targetVideo, string targetAudio) in VideoTargets)
            {
                string label = $"{Path.GetFileNameWithoutExtension(path)}-{container}-в-{name}";
                string output = Out($"{label}.{name}");

                ConversionResult result = await converter.ConvertAsync(Source.File(path), Target.File(output, to));

                bool encoded = Encoded[container].Contains(name);
                Assert.True(result.Succeeded);
                Assert.Equal(encoded, result.Reencoded);
                Assert.Same(to, await FileTypeDetector.DetectAsync(Source.File(output)));
                // The one mixed pair: VP9 fits MP4 and is copied, while Opus does not and is encoded.
                bool videoCopied = !encoded || (container == "webm" && name == "mp4");
                Assert.Equal(videoCopied ? video : targetVideo, await fixture.ProbeAsync(output, "stream=codec_name"));
                Assert.Equal(encoded ? targetAudio : audio, await fixture.ProbeAsync(output, "stream=codec_name", "a:0"));
                Assert.Equal("320x240", await fixture.SizeAsync(output));
                Assert.InRange(await fixture.DurationAsync(output), VideoRunFixture.Seconds - 0.4, VideoRunFixture.Seconds + 0.4);
                Keep(output, label);
                done++;
            }
        }

        Assert.Equal(25, done);
    }

    [ExternalFact]
    public async Task EveryVideoToSoundPair_File_ToFile_ReadsBack()
    {
        Converter converter = new();
        int done = 0;

        foreach ((string container, string path, _, _) in fixture.Inputs)
        {
            foreach ((FileFormat to, string name, string codec) in SoundTargets)
            {
                string label = $"{Path.GetFileNameWithoutExtension(path)}-{container}-в-{name}";
                string output = Out($"{label}.{name}");

                ConversionResult result = await converter.ConvertAsync(Source.File(path), Target.File(output, to));

                Assert.True(result.Succeeded);
                Assert.True(result.Reencoded);
                Assert.Same(to, await FileTypeDetector.DetectAsync(Source.File(output)));
                Assert.Equal(codec, await fixture.ProbeAsync(output, "stream=codec_name", "a:0"));
                Assert.InRange(await fixture.DurationAsync(output), VideoRunFixture.Seconds - 0.4, VideoRunFixture.Seconds + 0.4);
                Keep(output, label);
                done++;
            }
        }

        Assert.Equal(25, done);
    }

    [ExternalFact]
    public async Task Stream_WithoutRewinding_ToMemory_Works_ForEveryContainer()
    {
        Converter converter = new();

        foreach ((string container, string path, _, _) in fixture.Inputs)
        {
            await using NonSeekableFile source = new(path);

            ConversionResult result = await converter.ConvertAsync(Source.Stream(source), Target.Memory(FileFormat.Mkv));

            Assert.NotNull(result.Bytes);
            string output = Out($"поток-{container}.mkv");
            await File.WriteAllBytesAsync(output, result.Bytes);
            Assert.Same(FileFormat.Mkv, await FileTypeDetector.DetectAsync(Source.File(output)));
            Assert.Equal("320x240", await fixture.SizeAsync(output));
            Keep(output, $"поток-{container}-в-память-mkv");
        }
    }

    [ExternalFact]
    public async Task Folder_To_Folder_ThirtyClips_ParallelismNeverExceedsTheSetting()
    {
        foreach (int limit in new[] { 1, 4 })
        {
            string target = Out($"пакет-{limit}");
            Converter converter = new(new ConverterSettings { MaxParallelTasks = limit });
            using PeakWatcher watcher = new();

            BatchReport report = await converter.ConvertBatchAsync(Source.Folder(fixture.BatchFolder), Target.Folder(target, FileFormat.Webm));

            Assert.Equal(30, report.Total);
            Assert.Equal(30, report.Succeeded);
            Assert.Equal(30, Directory.GetFiles(target, "*.webm").Length);
            Assert.True(watcher.Peak <= limit, $"At limit {limit} there were {watcher.Peak} ffmpeg processes at once.");
            if (limit == 4)
            {
                Assert.True(watcher.Peak >= 2, $"At limit 4 no parallelism is seen: peak {watcher.Peak}.");
            }
        }
    }

    [ExternalFact]
    public async Task Cancel_InTheMiddleOfALongFile_StopsTheProcess_AndLeavesNoTempFiles()
    {
        string temp = Out("временная");
        Directory.CreateDirectory(temp);
        Converter converter = new(new ConverterSettings { TempFolder = temp });
        using CancellationTokenSource cts = new();
        List<double> percents = [];
        IProgress<ConversionProgress> progress = new InlineProgress<ConversionProgress>(p =>
        {
            percents.Add(p.Percent);
            if (p.Percent >= 3)
            {
                cts.Cancel();
            }
        });
        string target = Out("отменённое.webm");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            converter.ConvertAsync(Source.File(fixture.Long), Target.File(target, FileFormat.Webm), progress: progress, cancellationToken: cts.Token));

        await Task.Delay(1500);
        Assert.Empty(Process.GetProcessesByName("ffmpeg"));
        Assert.Empty(Directory.GetFiles(temp));
        Assert.False(File.Exists(target));
        Assert.NotEmpty(percents);
    }

    [ExternalFact]
    public async Task Broken_Liar_AndSoundOnlyInputs_AreHonestlyRefused_OrConvertedByTheirRealType()
    {
        Converter converter = new();

        ConversionException broken = await Assert.ThrowsAsync<ConversionException>(() =>
            converter.ConvertAsync(Source.File(fixture.Broken), Target.File(Out("никогда.mkv"), FileFormat.Mkv)));
        ConversionResult liar = await converter.ConvertAsync(Source.File(fixture.Liar), Target.File(Out("лжец.wav"), FileFormat.Wav));
        ConversionException liarToVideo = await Assert.ThrowsAsync<ConversionException>(() =>
            converter.ConvertAsync(Source.File(fixture.Liar), Target.File(Out("лжец.mp4"), FileFormat.Mp4)));
        ConversionException soundOnly = await Assert.ThrowsAsync<ConversionException>(() =>
            converter.ConvertAsync(Source.File(fixture.SoundOnly), Target.File(Out("без-картинки.mkv"), FileFormat.Mkv)));
        ConversionResult soundToMp3 = await converter.ConvertAsync(Source.File(fixture.SoundOnly), Target.File(Out("только-звук.mp3"), FileFormat.Mp3));

        Assert.Equal(ConversionFailureReason.ToolFailed, broken.Reason);
        Assert.False(File.Exists(Out("никогда.mkv")));
        Assert.Same(FileFormat.Mp3, liar.SourceType); // an MP3 under the name .mp4
        Assert.Equal(ConversionFailureReason.UnsupportedConversion, liarToVideo.Reason);
        Assert.Equal(ConversionFailureReason.ToolFailed, soundOnly.Reason);
        Assert.Same(FileFormat.Mp4, soundToMp3.SourceType);
        Keep(Out("лжец.wav"), "лжец-mp3-под-именем-mp4-в-wav");
        Keep(Out("только-звук.mp3"), "только-звук-mp4-в-mp3");
    }

    [ExternalFact]
    public async Task Progress_Grows_NeverRepeats_AndEndsAt100()
    {
        List<double> percents = [];
        IProgress<ConversionProgress> progress = new InlineProgress<ConversionProgress>(p => percents.Add(p.Percent));
        ConversionOptions options = new() { Video = new VideoOptions { Reencode = true } };

        await new Converter().ConvertAsync(
            Source.File(fixture.Inputs.Single(i => i.Container == "mkv").Path), Target.File(Out("ход.webm"), FileFormat.Webm), options, progress);

        Assert.True(percents.Count >= 3, $"Too few values: {string.Join(", ", percents)}");
        Assert.Equal(0, percents[0]);
        Assert.Equal(100, percents[^1]);
        Assert.Equal(percents.Order().ToArray(), percents);
        Assert.Equal(percents.Distinct().Count(), percents.Count);
    }

    [ExternalFact]
    public async Task Settings_AreVisibleInTheResult_ReadByFfprobe()
    {
        Converter converter = new();
        string source = fixture.Inputs.Single(i => i.Container == "mp4").Path;
        string low = Out("качество-10.mp4");
        string high = Out("качество-90.mp4");
        string bitrate = Out("битрейт-300к.mp4");
        string size = Out("размер-160.mp4");
        string rate = Out("кадры-10.mp4");
        string forced = Out("перекодировано.mp4");
        string rotated = Out("поворот-вправо.mp4");
        string scaled = Out("масштаб-50.mp4");

        ConversionResult lowResult = await converter.ConvertAsync(Source.File(source), Target.File(low, FileFormat.Mp4), new ConversionOptions { Video = new VideoOptions { Quality = 10 } });
        await converter.ConvertAsync(Source.File(source), Target.File(high, FileFormat.Mp4), new ConversionOptions { Video = new VideoOptions { Quality = 90 } });
        await converter.ConvertAsync(Source.File(source), Target.File(bitrate, FileFormat.Mp4), new ConversionOptions { Video = new VideoOptions { Bitrate = 300_000 } });
        await converter.ConvertAsync(Source.File(source), Target.File(size, FileFormat.Mp4), new ConversionOptions { Video = new VideoOptions { Width = 160 } });
        await converter.ConvertAsync(Source.File(source), Target.File(rate, FileFormat.Mp4), new ConversionOptions { Video = new VideoOptions { FrameRate = 10 } });
        ConversionResult forcedResult = await converter.ConvertAsync(Source.File(source), Target.File(forced, FileFormat.Mp4), new ConversionOptions { Video = new VideoOptions { Reencode = true } });
        await converter.ConvertAsync(Source.File(source), Target.File(rotated, FileFormat.Mp4), new ConversionOptions { Operations = { Operation.RotateRight } });
        await converter.ConvertAsync(Source.File(source), Target.File(scaled, FileFormat.Mp4), new ConversionOptions { Operations = { Operation.ScalePercent(50) } });

        Assert.True(lowResult.Reencoded);
        Assert.True(new FileInfo(low).Length < new FileInfo(high).Length, "Quality 10 must give a smaller file than 90.");
        double bitRate = double.Parse(await fixture.ProbeAsync(bitrate, "stream=bit_rate"), System.Globalization.CultureInfo.InvariantCulture);
        Assert.InRange(bitRate, 300_000 * 0.7, 300_000 * 1.3);
        Assert.Equal("160x120", await fixture.SizeAsync(size));
        Assert.Equal("10/1", await fixture.ProbeAsync(rate, "stream=r_frame_rate"));
        Assert.True(forcedResult.Reencoded);
        Assert.Equal("240x320", await fixture.SizeAsync(rotated));
        Assert.Equal("160x120", await fixture.SizeAsync(scaled));
        foreach (string file in new[] { low, high, bitrate, size, rate, forced, rotated, scaled })
        {
            Keep(file, Path.GetFileNameWithoutExtension(file));
        }
    }

    [ExternalFact]
    public async Task SpecialSamples_NoSound_RotationFlag_OddSize_VariableFrameRate()
    {
        Converter converter = new();
        ConversionOptions encode = new() { Video = new VideoOptions { Reencode = true } };

        ConversionResult silent = await converter.ConvertAsync(Source.File(fixture.NoSound), Target.File(Out("без-звука-в-mkv.mkv"), FileFormat.Mkv));
        await converter.ConvertAsync(Source.File(fixture.NoSound), Target.File(Out("без-звука-в-webm.webm"), FileFormat.Webm));
        string kept = Out("повёрнутое-как-есть.mp4");
        string turned = Out("повёрнутое-перекодировано.mp4");
        await converter.ConvertAsync(Source.File(fixture.Rotated), Target.File(kept, FileFormat.Mp4));
        await converter.ConvertAsync(Source.File(fixture.Rotated), Target.File(turned, FileFormat.Mp4), encode);
        string oddCopied = Out("нечётное-как-есть.mp4");
        string oddEncoded = Out("нечётное-перекодировано.mkv");
        await converter.ConvertAsync(Source.File(fixture.Odd), Target.File(oddCopied, FileFormat.Mp4));
        await converter.ConvertAsync(Source.File(fixture.Odd), Target.File(oddEncoded, FileFormat.Mkv), encode);
        string variable = Out("переменная-в-mp4.mp4");
        ConversionResult variableResult = await converter.ConvertAsync(Source.File(fixture.Variable), Target.File(variable, FileFormat.Mp4), encode);

        Assert.False(silent.Reencoded);
        Assert.Equal(string.Empty, await fixture.ProbeAsync(Out("без-звука-в-mkv.mkv"), "stream=codec_name", "a:0"));
        Assert.Equal(string.Empty, await fixture.ProbeAsync(Out("без-звука-в-webm.webm"), "stream=codec_name", "a:0"));
        // A copied rotated video keeps its flag (the pixels stay 320x240); an encoded one is turned for real.
        Assert.Equal("320x240", await fixture.SizeAsync(kept));
        Assert.Equal("240x320", await fixture.SizeAsync(turned));
        Assert.Equal("641x361", await fixture.SizeAsync(oddCopied));
        Assert.Equal("640x360", await fixture.SizeAsync(oddEncoded));
        Assert.True(variableResult.Reencoded);
        Assert.InRange(await fixture.DurationAsync(variable), VideoRunFixture.Seconds - 0.6, VideoRunFixture.Seconds + 0.6);
        foreach (string file in new[] { Out("без-звука-в-mkv.mkv"), Out("без-звука-в-webm.webm"), kept, turned, oddCopied, oddEncoded, variable })
        {
            Keep(file, Path.GetFileNameWithoutExtension(file));
        }
    }
}
