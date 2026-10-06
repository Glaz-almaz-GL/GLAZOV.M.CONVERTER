using System.Diagnostics;
using GLAZOV.M.CONVERTER.Batch;
using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Detection;
using GLAZOV.M.CONVERTER.Errors;
using GLAZOV.M.CONVERTER.Ffmpeg.Engines;
using GLAZOV.M.CONVERTER.Ffmpeg.Progress;
using GLAZOV.M.CONVERTER.Formats;
using GLAZOV.M.CONVERTER.Settings;

namespace GLAZOV.M.CONVERTER.Ffmpeg.Tests.Live;

/// <summary>
/// Прогон изображений на настоящих файлах через публичные вызовы эталона и настоящий FFmpeg: все пары форматов,
/// файл, поток без перемотки, память, папка параллельно, отмена, поворот, масштаб, качество, прозрачность, испорченный
/// и «лживый» входы. Результаты читаются обратно настоящим ffprobe. Если переменная CONVERTER_RUN_KEEP указывает папку —
/// результаты сохраняются туда для просмотра. Нет FFmpeg — проверки пропускаются, а не проходят.
/// </summary>
[Collection("FFmpeg")]
public sealed class ImageRunTests(ImageFixture fixture) : IClassFixture<ImageFixture>, IDisposable
{
    private static readonly (FileFormat Format, string Codec)[] Targets =
    [
        (FileFormat.Png, "png"), (FileFormat.Jpeg, "mjpeg"), (FileFormat.Webp, "webp"), (FileFormat.Bmp, "bmp"),
        (FileFormat.Gif, "gif"), (FileFormat.Tiff, "tiff"), (FileFormat.Tga, "targa"),
    ];

    private readonly string _work = Path.Combine(Path.GetTempPath(), "convrun-img-out-" + Guid.NewGuid().ToString("N"));

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

        Directory.CreateDirectory(keep);
        File.Copy(produced, Path.Combine(keep, label + Path.GetExtension(produced)), overwrite: true);
    }

    [ExternalFact]
    public async Task EveryRealInput_ToEveryFormat_File_ToFile()
    {
        Converter converter = FfmpegConverter.Create();
        int done = 0;

        foreach (ImageFixture.Input input in fixture.Inputs)
        {
            foreach ((FileFormat to, string codec) in Targets)
            {
                string label = Path.GetFileNameWithoutExtension(input.Path) + "-" + input.Format.Name + "-в-" + to.Name;
                string output = Out($"{label}.{to.Extension}");

                ConversionResult result = await converter.ConvertAsync(Source.File(input.Path), Target.File(output, to));

                Assert.True(result.Succeeded);
                Assert.Same(input.Format, result.SourceType);
                (string actualCodec, int width, int height) = await fixture.ProbeAsync(output);
                Assert.Equal(codec, actualCodec);
                Assert.Equal((input.Width, input.Height), (width, height));
                if (!to.Equals(FileFormat.Tga))
                {
                    Assert.Same(to, await FfmpegConverter.DetectAsync(Source.File(output)));
                }

                Keep(output, label);
                done++;
            }
        }

        Assert.Equal(63, done);
    }

    [ExternalFact]
    public async Task Stream_WithoutSeek_ToStream_ForTheHardInputs()
    {
        // TIFF, DDS и PSD из канала FFmpeg не читает — библиотека должна справляться сама.
        Converter converter = FfmpegConverter.Create();

        foreach (ImageFixture.Input input in fixture.Inputs.Where(i => i.Format.Name is "TIFF" or "DDS" or "PSD"))
        {
            await using NonSeekableFile source = new(input.Path);
            using MemoryStream output = new();

            await converter.ConvertAsync(Source.Stream(source), Target.Stream(output, FileFormat.Png));

            output.Position = 0;
            Assert.Same(FileFormat.Png, await FfmpegConverter.DetectAsync(Source.Stream(output)));
        }
    }

    [ExternalFact]
    public async Task File_ToMemory_GivesBytesOfThePng()
    {
        ConversionResult result = await FfmpegConverter.Create().ConvertAsync(Source.File(fixture.Photo.Path), Target.Memory(FileFormat.Webp));

        Assert.NotNull(result.Bytes);
        using MemoryStream bytes = new(result.Bytes);
        Assert.Same(FileFormat.Webp, await FfmpegConverter.DetectAsync(Source.Stream(bytes)));
    }

    [ExternalFact]
    public async Task RotateAndScale_GiveTheRightSizes_OnARealPhoto()
    {
        string output = Out("поворот-масштаб.jpg");

        await FfmpegConverter.Create().ConvertAsync(
            Source.File(fixture.Photo.Path),
            Target.File(output, FileFormat.Jpeg),
            new ConversionOptions { Operations = { Operation.RotateRight, Operation.ScalePercent(50) }, Image = new ImageOptions { Quality = 85 } });

        (string codec, int width, int height) = await fixture.ProbeAsync(output);
        Assert.Equal("mjpeg", codec);
        Assert.Equal((240, 320), (width, height)); // 640×480 → 480×640 → 240×320
        Keep(output, "фото-вправо-50-процентов");
    }

    [ExternalFact]
    public async Task JpegQuality_IsVisibleInTheSize()
    {
        string low = Out("качество-10.jpg");
        string high = Out("качество-95.jpg");
        Converter converter = FfmpegConverter.Create();

        await converter.ConvertAsync(Source.File(fixture.Photo.Path), Target.File(low, FileFormat.Jpeg), new ConversionOptions { Image = new ImageOptions { Quality = 10 } });
        await converter.ConvertAsync(Source.File(fixture.Photo.Path), Target.File(high, FileFormat.Jpeg), new ConversionOptions { Image = new ImageOptions { Quality = 95 } });

        Assert.True(new FileInfo(low).Length * 2 < new FileInfo(high).Length, "Качество 10 должно давать файл заметно меньше, чем 95.");
        Keep(low, "качество-10");
        Keep(high, "качество-95");
    }

    [ExternalFact]
    public async Task TransparentPng_ToJpeg_BecomesWhite_AndToPng_KeepsTheTransparency()
    {
        string jpeg = Out("прозрачный-в-jpeg.jpg");
        string png = Out("прозрачный-в-png.png");
        Converter converter = FfmpegConverter.Create();

        await converter.ConvertAsync(Source.File(fixture.Transparent.Path), Target.File(jpeg, FileFormat.Jpeg));
        await converter.ConvertAsync(Source.File(fixture.Transparent.Path), Target.File(png, FileFormat.Png));

        byte[] flat = await fixture.FirstPixelAsync(jpeg);
        byte[] alpha = await fixture.FirstPixelAsync(png, "rgba");
        Assert.True(flat[0] > 120 && flat[1] > 150 && flat[2] > 190, $"Полупрозрачный синий на белом: {flat[0]},{flat[1]},{flat[2]}");
        Assert.True(alpha[3] is > 80 and < 120, $"Прозрачность PNG сохранена (альфа около 102): {alpha[3]}");
        Keep(jpeg, "прозрачный-на-белом");
    }

    [ExternalFact]
    public async Task AnimatedGif_GivesTheFirstFrameOnly()
    {
        string output = Out("кадр.png");
        ImageFixture.Input gif = fixture.Inputs.Single(i => i.Format.Equals(FileFormat.Gif));

        await FfmpegConverter.Create().ConvertAsync(Source.File(gif.Path), Target.File(output, FileFormat.Png));

        (string codec, int width, int height) = await fixture.ProbeAsync(output);
        Assert.Equal("png", codec);
        Assert.Equal((160, 120), (width, height));
    }

    [ExternalFact]
    public async Task BrokenLiarAndTga_AreHonestlyRefused_OrConvertedByTheirRealType()
    {
        Converter converter = FfmpegConverter.Create();

        ConversionException broken = await Assert.ThrowsAsync<ConversionException>(async () =>
            await converter.ConvertAsync(Source.File(fixture.BrokenPng), Target.File(Out("никогда.jpg"), FileFormat.Jpeg)));
        ConversionException tga = await Assert.ThrowsAsync<ConversionException>(async () =>
            await converter.ConvertAsync(Source.File(fixture.TgaFile), Target.File(Out("tga-в-png.png"), FileFormat.Png)));
        ConversionResult liar = await converter.ConvertAsync(Source.File(fixture.LiarJpg), Target.File(Out("лжец.webp"), FileFormat.Webp));

        Assert.Equal(ConversionFailureReason.TypeNotConfirmed, broken.Reason);
        Assert.Equal(ConversionFailureReason.TypeNotConfirmed, tga.Reason);
        Assert.Contains("фото.tga", tga.Message);
        Assert.Same(FileFormat.Png, liar.SourceType); // PNG под именем .jpg
        Assert.False(File.Exists(Out("никогда.jpg")));
    }

    [ExternalFact]
    public async Task Folder_To_Folder_FortyImages_ParallelismNeverExceedsTheSetting()
    {
        foreach (int limit in new[] { 1, 4 })
        {
            string target = Out($"пакет-{limit}");
            Converter converter = FfmpegConverter.Create(new ConverterSettings { MaxParallelTasks = limit });
            using PeakWatcher watcher = new();

            BatchReport report = await converter.ConvertBatchAsync(Source.Folder(fixture.BatchFolder), Target.Folder(target, FileFormat.Jpeg));

            Assert.Equal(40, report.Total);
            Assert.Equal(40, report.Succeeded);
            Assert.Equal(40, Directory.GetFiles(target, "*.jpg").Length);
            Assert.True(watcher.Peak <= limit, $"При лимите {limit} одновременно работало {watcher.Peak} процессов FFmpeg.");
            if (limit == 4)
            {
                Assert.True(watcher.Peak >= 2, $"При лимите 4 параллельности не видно: пик {watcher.Peak}.");
            }
        }
    }

    [ExternalFact]
    public async Task Batch_Cancelled_Midway_LeavesReportNoProcessesAndNoTempFiles()
    {
        string target = Out("отмена");
        Converter converter = FfmpegConverter.Create(new ConverterSettings { MaxParallelTasks = 2 });
        using CancellationTokenSource cts = new();
        IProgress<BatchProgress> progress = new InlineProgress<BatchProgress>(p =>
        {
            if (p.Completed >= 3)
            {
                cts.Cancel();
            }
        });

        BatchReport report = await converter.ConvertBatchAsync(Source.Folder(fixture.BatchFolder), Target.Folder(target, FileFormat.Jpeg), progress, cts.Token);

        Assert.Equal(40, report.Total);
        Assert.True(report.Canceled > 0, "Отмена не дошла до записей отчёта.");
        Assert.Equal(40, report.Succeeded + report.Failed + report.Canceled);
        await Task.Delay(1500);
        Assert.Empty(Process.GetProcessesByName("ffmpeg"));
        Assert.Empty(Directory.GetFiles(Path.GetTempPath(), "convert-*.in.*"));
        Assert.Empty(Directory.GetFiles(Path.GetTempPath(), "convert-*.out.*"));
    }

    [ExternalFact]
    public async Task Batch_WithBrokenLiarAndGoodImages_ReportsEachHonestly()
    {
        string target = Out("смесь");
        Source[] sources =
        [
            Source.File(fixture.Photo.Path), Source.File(fixture.BrokenPng), Source.File(fixture.LiarJpg), Source.File(fixture.Transparent.Path),
        ];

        BatchReport report = await FfmpegConverter.Create().ConvertBatchAsync(sources, Target.Folder(target, FileFormat.Webp));

        Assert.Equal(4, report.Total);
        Assert.Equal(3, report.Succeeded);
        BatchItemResult failed = Assert.Single(report.Items, i => !i.Succeeded);
        Assert.Equal("битый.png", failed.SourceName);
        Assert.Equal(ConversionFailureReason.TypeNotConfirmed, failed.Reason);
        Assert.Single(report.FailedSources());
    }
}
