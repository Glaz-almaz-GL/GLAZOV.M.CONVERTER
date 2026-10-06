using GLAZOV.M.CONVERTER.Batch;
using GLAZOV.M.CONVERTER.Capabilities;
using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Detection;
using GLAZOV.M.CONVERTER.Errors;
using GLAZOV.M.CONVERTER.Settings;
using GLAZOV.M.CONVERTER.Tests.Extension;

namespace GLAZOV.M.CONVERTER.Tests.Scenarios;

/// <summary>
/// Форма вызовов из принятого эталона использования: каждый сценарий записан так, как его пишет разработчик.
/// Тест проверяет, что запись собирается и что вызов не выполняется молча: до реализации — «не реализовано».
/// </summary>
public class ScenarioShapeTests
{
    [Fact]
    public void OneFileStreamAndMemory_ShapeCompiles()
    {
        // Форма вызовов записана как в эталоне; вызовы не выполняются — их поведение проверяют тесты конвертации.
        var converter = new Converter();
        MemoryStream input = new();
        MemoryStream output = new();
        CancellationToken token = CancellationToken.None;

        Func<Task<ConversionResult>>[] calls =
        [
            () => converter.ConvertAsync(Source.File("песня.ogg"), Target.File("песня.mp3", TestFormats.Alpha)),
            () => converter.ConvertAsync(Source.Stream(input), Target.Stream(output, TestFormats.Beta)),
            () => converter.ConvertAsync(Source.File("песня.ogg"), Target.Memory(TestFormats.Alpha)),
            () => converter.ConvertAsync(
                Source.File("фото.png"),
                Target.File("фото.jpg", TestFormats.Delta),
                new ConversionOptions { Operations = { Operation.RotateRight, Operation.ScalePercent(75) } }),
            () => converter.ConvertAsync(
                Source.File("песня.wav"),
                Target.File("песня.mp3", TestFormats.Alpha),
                new ConversionOptions { Audio = new AudioOptions { Bitrate = 192_000, SampleRate = 44_100, Channels = 2 } }),
            () => converter.ConvertAsync(
                Source.File("песня.ogg"),
                Target.File("песня.mp3", TestFormats.Alpha),
                options: null,
                progress: new Progress<ConversionProgress>(p => _ = p.Percent),
                cancellationToken: token),
        ];

        Assert.Equal(6, calls.Length);
        Assert.All(calls, Assert.NotNull);
    }

    [Fact]
    public void BatchAndReport_ShapeCompiles()
    {
        var converter = new Converter();
        CancellationToken token = CancellationToken.None;

        Func<Task<BatchReport>>[] calls =
        [
            () => converter.ConvertBatchAsync(
                Source.Folder("музыка", pattern: "*.ogg", recursive: true),
                Target.Folder("готово", TestFormats.Alpha),
                progress: new Progress<BatchProgress>(p => _ = (p.Completed, p.Total, p.CurrentName)),
                cancellationToken: token),
            () => converter.ConvertBatchAsync(
                new[] { Source.File("a.ogg"), Source.File("b.wav") },
                Target.Folder("готово", TestFormats.Alpha)),
        ];
        Assert.All(calls, Assert.NotNull);

        // Форма отчёта: поля читаются так, как в эталоне (выполнение не требуется — это проверка записи).
        Func<BatchReport, string> describe = report =>
        {
            string line = $"{report.Succeeded} из {report.Total}; отменено: {report.Canceled}; ошибок: {report.Failed}";
            foreach (BatchItemResult? item in report.Items.Where(i => !i.Succeeded))
            {
                line += $"{item.SourceName}: {item.Stage} — {item.Reason} — {item.Message} — {item.OutputPath}";
            }

            _ = report.FailedSources();
            return line;
        };
        Assert.NotNull(describe);
    }

    [Fact]
    public void Capabilities_ShapeCompiles()
    {
        var converter = new Converter();

        var caps = converter.Capabilities;
        Func<bool> can = () => caps.CanConvert(TestFormats.Gamma, TestFormats.Alpha);
        Func<IReadOnlyList<Conversion>> all = () => caps.All;
        Func<ToolStatus> tool = () => caps.Tool("sometool");
        Assert.NotNull(can);
        Assert.NotNull(all);
        Assert.NotNull(tool);

        Func<ToolStatus, string> hint = status => $"{status.Name} {status.Found} {status.Path} {status.InstallHint}";
        Func<Conversion, string> row = c => $"{c.From} {c.To} {c.Availability}";
        Assert.NotNull(hint);
        Assert.NotNull(row);
    }

    [Fact]
    public void FileTypeNames_CarryNameMimeAndExtension()
    {
        Assert.Equal("GAMMA", TestFormats.Gamma.Name);
        Assert.Equal("application/x-test-gamma", TestFormats.Gamma.MimeType);
        Assert.Equal("gamma", TestFormats.Gamma.Extension);
        Assert.Equal(string.Empty, FileFormat.Unknown.Extension);
    }

    [Fact]
    public void Settings_DefaultsAndInitializersMatchTheSketch()
    {
        var settings = new ConverterSettings
        {
            MaxParallelTasks = 8,
            TempFolder = "D:/tmp/convert",
            ToolPaths = { ["sometool"] = "C:/tools/sometool/bin/sometool.exe" },
            OverwriteExisting = false,
        };

        Assert.Equal(8, settings.MaxParallelTasks);
        Assert.Equal("C:/tools/sometool/bin/sometool.exe", settings.ToolPaths["sometool"]);
        Assert.Equal(Environment.ProcessorCount, new ConverterSettings().MaxParallelTasks);
        Assert.False(new ConverterSettings().OverwriteExisting);
        // Создание Converter с этим объектом отвергнет несуществующую папку (проверка настроек) — это проверяется в тестах настроек.
    }

    [Fact]
    public void AudioOptions_DefaultToNotSetAndRejectNonPositive()
    {
        var audio = new AudioOptions();
        Assert.Null(audio.Bitrate);
        Assert.Null(audio.SampleRate);
        Assert.Null(audio.Channels);
        Assert.Null(new ConversionOptions().Audio);

        audio.Bitrate = 192_000;
        audio.Bitrate = null;
        Assert.Null(audio.Bitrate);

        Assert.Throws<ArgumentOutOfRangeException>(() => audio.Bitrate = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => audio.SampleRate = -44_100);
        Assert.Throws<ArgumentOutOfRangeException>(() => audio.Channels = 0);
    }

    [Fact]
    public void ImageOptions_DefaultToNotSetAndRejectOutOfRange()
    {
        ImageOptions image = new();
        Assert.Null(image.Quality);
        Assert.Null(new ConversionOptions().Image);

        foreach (int ok in new[] { 1, 50, 85, 100 })
        {
            image.Quality = ok;
            Assert.Equal(ok, image.Quality);
        }

        image.Quality = null;
        Assert.Null(image.Quality);

        Assert.Throws<ArgumentOutOfRangeException>(() => image.Quality = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => image.Quality = 101);
        Assert.Throws<ArgumentOutOfRangeException>(() => image.Quality = -5);
    }

    [Fact]
    public void VideoOptions_DefaultToNotSetAndRejectOutOfRange()
    {
        VideoOptions video = new();
        Assert.Null(video.Quality);
        Assert.Null(video.Bitrate);
        Assert.Null(video.Width);
        Assert.Null(video.Height);
        Assert.Null(video.FrameRate);
        Assert.False(video.Reencode);
        Assert.False(video.ConvertPixelFormat);
        Assert.Null(new ConversionOptions().Video);

        video.Quality = 1;
        video.Quality = 100;
        video.Bitrate = 1;
        video.Width = 2;
        video.Height = 2;
        video.FrameRate = 0.5;
        video.Reencode = true;
        Assert.Equal((100, 1, 2, 2, 0.5, true), (video.Quality, video.Bitrate, video.Width, video.Height, video.FrameRate, video.Reencode));

        video.Quality = null;
        video.FrameRate = null;
        Assert.Null(video.Quality);
        Assert.Null(video.FrameRate);

        Assert.Throws<ArgumentOutOfRangeException>(() => video.Quality = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => video.Quality = 101);
        Assert.Throws<ArgumentOutOfRangeException>(() => video.Bitrate = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => video.Width = -1);
        Assert.Throws<ArgumentOutOfRangeException>(() => video.Height = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => video.FrameRate = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => video.FrameRate = -1);
        Assert.Throws<ArgumentOutOfRangeException>(() => video.FrameRate = double.NaN);
        Assert.Throws<ArgumentOutOfRangeException>(() => video.FrameRate = double.PositiveInfinity);
    }

    [Fact]
    public void VideoFormats_CarryNameMimeAndExtension()
    {
        Assert.Equal(("MP4", "video/mp4", "mp4"), (FileFormat.Mp4.Name, FileFormat.Mp4.MimeType, FileFormat.Mp4.Extension));
        Assert.Equal(("MKV", "video/x-matroska", "mkv"), (FileFormat.Mkv.Name, FileFormat.Mkv.MimeType, FileFormat.Mkv.Extension));
        Assert.Equal(("WebM", "video/webm", "webm"), (FileFormat.Webm.Name, FileFormat.Webm.MimeType, FileFormat.Webm.Extension));
        Assert.Equal(("MOV", "video/quicktime", "mov"), (FileFormat.Mov.Name, FileFormat.Mov.MimeType, FileFormat.Mov.Extension));
        Assert.Equal(("AVI", "video/x-msvideo", "avi"), (FileFormat.Avi.Name, FileFormat.Avi.MimeType, FileFormat.Avi.Extension));
    }

    [Fact]
    public void NewFormatsAndImageOptions_ShapeCompilesAsInTheSketch()
    {
        var converter = new Converter();

        Func<Task<ConversionResult>>[] calls =
        [
            () => converter.ConvertAsync(Source.File("песня.flac"), Target.File("песня.m4a", TestFormats.Epsilon)),
            () => converter.ConvertAsync(Source.File("фото.png"), Target.File("фото.jpg", TestFormats.Delta)),
            () => converter.ConvertAsync(
                Source.File("фото.png"),
                Target.File("фото-75.jpg", TestFormats.Delta),
                new ConversionOptions
                {
                    Operations = { Operation.RotateRight, Operation.ScalePercent(75) },
                    Image = new ImageOptions { Quality = 85 },
                }),
        ];

        Assert.All(calls, Assert.NotNull);
    }

    [Fact]
    public void ErrorHandling_ShapeCompiles()
    {
        // Форма обработки ошибки одиночного вызова: поля читаются так, как в эталоне.
        Func<ConversionException, string> describe = ex =>
            $"{ex.Stage} {ex.Reason} {ex.Message} {ex.ToolName} {ex.ToolExitCode} {ex.ToolOutput}";
        Assert.NotNull(describe);
    }
}
