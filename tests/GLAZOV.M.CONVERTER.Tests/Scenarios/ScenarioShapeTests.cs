using GLAZOV.M.CONVERTER;

namespace GLAZOV.M.CONVERTER.Tests;

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
        using var input = new MemoryStream();
        using var output = new MemoryStream();
        var token = CancellationToken.None;

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
        var token = CancellationToken.None;

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
            var line = $"{report.Succeeded} из {report.Total}; отменено: {report.Canceled}; ошибок: {report.Failed}";
            foreach (var item in report.Items.Where(i => !i.Succeeded))
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
        using var input = new MemoryStream();

        await Assert.ThrowsAsync<NotImplementedException>(() => FileTypeDetector.DetectAsync(Source.File("загадка.dat")));
        await Assert.ThrowsAsync<NotImplementedException>(() => FileTypeDetector.DetectAsync(Source.Stream(input)));

        var caps = converter.Capabilities;
        Assert.Throws<NotImplementedException>(() => caps.CanConvert(FileType.Ogg, Format.Mp3));
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
        Assert.Equal("OGG", FileType.Ogg.Name);
        Assert.Equal("audio/ogg", FileType.Ogg.MimeType);
        Assert.Equal("ogg", FileType.Ogg.Extension);
        Assert.Equal(string.Empty, FileType.Unknown.Extension);
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
        Assert.NotNull(new Converter(settings));
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
