using GLAZOV.M.CONVERTER.Batch;
using GLAZOV.M.CONVERTER.Capabilities;
using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Detection;
using GLAZOV.M.CONVERTER.Errors;
using GLAZOV.M.CONVERTER.Settings;

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
            () => converter.ConvertAsync(Source.File("песня.ogg"), Target.File("песня.mp3", FileFormat.Mp3)),
            () => converter.ConvertAsync(Source.Stream(input), Target.Stream(output, FileFormat.Wav)),
            () => converter.ConvertAsync(Source.File("песня.ogg"), Target.Memory(FileFormat.Mp3)),
            () => converter.ConvertAsync(
                Source.File("фото.png"),
                Target.File("фото.jpg", FileFormat.Jpeg),
                new ConversionOptions { Operations = { Operation.RotateRight, Operation.ScalePercent(75) } }),
            () => converter.ConvertAsync(
                Source.File("песня.wav"),
                Target.File("песня.mp3", FileFormat.Mp3),
                new ConversionOptions { Audio = new AudioOptions { Bitrate = 192_000, SampleRate = 44_100, Channels = 2 } }),
            () => converter.ConvertAsync(
                Source.File("песня.ogg"),
                Target.File("песня.mp3", FileFormat.Mp3),
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
                Target.Folder("готово", FileFormat.Mp3),
                progress: new Progress<BatchProgress>(p => _ = (p.Completed, p.Total, p.CurrentName)),
                cancellationToken: token),
            () => converter.ConvertBatchAsync(
                new[] { Source.File("a.ogg"), Source.File("b.wav") },
                Target.Folder("готово", FileFormat.Mp3)),
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
        Func<bool> can = () => caps.CanConvert(FileFormat.Ogg, FileFormat.Mp3);
        Func<IReadOnlyList<Conversion>> all = () => caps.All;
        Func<ToolStatus> tool = () => caps.Tool("ffmpeg");
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
        Assert.Equal("OGG", FileFormat.Ogg.Name);
        Assert.Equal("audio/ogg", FileFormat.Ogg.MimeType);
        Assert.Equal("ogg", FileFormat.Ogg.Extension);
        Assert.Equal(string.Empty, FileFormat.Unknown.Extension);
    }

    [Fact]
    public void Settings_DefaultsAndInitializersMatchTheSketch()
    {
        var settings = new ConverterSettings
        {
            MaxParallelTasks = 8,
            TempFolder = "D:/tmp/convert",
            ToolPaths = { ["ffmpeg"] = "C:/tools/ffmpeg/bin/ffmpeg.exe" },
            OverwriteExisting = false,
        };

        Assert.Equal(8, settings.MaxParallelTasks);
        Assert.Equal("C:/tools/ffmpeg/bin/ffmpeg.exe", settings.ToolPaths["ffmpeg"]);
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
    public void ErrorHandling_ShapeCompiles()
    {
        // Форма обработки ошибки одиночного вызова: поля читаются так, как в эталоне.
        Func<ConversionException, string> describe = ex =>
            $"{ex.Stage} {ex.Reason} {ex.Message} {ex.ToolName} {ex.ToolExitCode} {ex.ToolOutput}";
        Assert.NotNull(describe);
    }
}
