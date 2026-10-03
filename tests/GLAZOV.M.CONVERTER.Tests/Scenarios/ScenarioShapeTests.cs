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
    public async Task OneFileStreamAndMemory_ShapeCompilesAndStubsThrow()
    {
        var converter = new Converter();
        using MemoryStream input = new();
        using MemoryStream output = new();
        CancellationToken token = CancellationToken.None;

        await Assert.ThrowsAsync<NotImplementedException>(() =>
            converter.ConvertAsync(Source.File("песня.ogg"), Target.File("песня.mp3", Format.Mp3)));
        await Assert.ThrowsAsync<NotImplementedException>(() =>
            converter.ConvertAsync(Source.Stream(input), Target.Stream(output, Format.Wav)));
        await Assert.ThrowsAsync<NotImplementedException>(() =>
            converter.ConvertAsync(Source.File("песня.ogg"), Target.Memory(Format.Mp3)));
        await Assert.ThrowsAsync<NotImplementedException>(() =>
            converter.ConvertAsync(
                Source.File("фото.png"),
                Target.File("фото.jpg", Format.Jpeg),
                new ConversionOptions { Operations = { Operation.RotateRight, Operation.ScalePercent(75) } }));
        await Assert.ThrowsAsync<NotImplementedException>(() =>
            converter.ConvertAsync(
                Source.File("песня.wav"),
                Target.File("песня.mp3", Format.Mp3),
                new ConversionOptions { Audio = new AudioOptions { Bitrate = 192_000, SampleRate = 44_100, Channels = 2 } }));
        await Assert.ThrowsAsync<NotImplementedException>(() =>
            converter.ConvertAsync(
                Source.File("песня.ogg"),
                Target.File("песня.mp3", Format.Mp3),
                options: null,
                progress: new Progress<ConversionProgress>(p => _ = p.Percent),
                cancellationToken: token));
    }

    [Fact]
    public async Task BatchAndReport_ShapeCompilesAndStubsThrow()
    {
        var converter = new Converter();
        CancellationToken token = CancellationToken.None;

        await Assert.ThrowsAsync<NotImplementedException>(() =>
            converter.ConvertBatchAsync(
                Source.Folder("музыка", pattern: "*.ogg", recursive: true),
                Target.Folder("готово", Format.Mp3),
                progress: new Progress<BatchProgress>(p => _ = (p.Completed, p.Total, p.CurrentName)),
                cancellationToken: token));
        await Assert.ThrowsAsync<NotImplementedException>(() =>
            converter.ConvertBatchAsync(
                new[] { Source.File("a.ogg"), Source.File("b.wav") },
                Target.Folder("готово", Format.Mp3)));

        // Форма отчёта: поля читаются так, как в эталоне (в тест не попадает выполнение — отчёта пока никто не создаёт).
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
    public async Task DetectAndCapabilities_ShapeCompilesAndStubsThrow()
    {
        var converter = new Converter();
        using MemoryStream input = new();

        await Assert.ThrowsAsync<NotImplementedException>(() => FileTypeDetector.DetectAsync(Source.File("загадка.dat")));
        await Assert.ThrowsAsync<NotImplementedException>(() => FileTypeDetector.DetectAsync(Source.Stream(input)));

        var caps = converter.Capabilities;
        Assert.Throws<NotImplementedException>(() => caps.CanConvert(Format.Ogg, Format.Mp3));
        Assert.Throws<NotImplementedException>(() => caps.All);
        Assert.Throws<NotImplementedException>(() => caps.Tool("ffmpeg"));

        Func<ToolStatus, string> hint = status => $"{status.Name} {status.Found} {status.Path} {status.InstallHint}";
        Func<Conversion, string> row = c => $"{c.From} {c.To} {c.Availability}";
        Assert.NotNull(hint);
        Assert.NotNull(row);
    }

    [Fact]
    public void FileTypeNames_CarryNameMimeAndExtension()
    {
        Assert.Equal("OGG", Format.Ogg.Name);
        Assert.Equal("audio/ogg", Format.Ogg.MimeType);
        Assert.Equal("ogg", Format.Ogg.Extension);
        Assert.Equal(string.Empty, Format.Unknown.Extension);
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
