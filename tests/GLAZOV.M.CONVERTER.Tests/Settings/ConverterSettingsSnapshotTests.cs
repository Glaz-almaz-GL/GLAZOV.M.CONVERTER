using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Settings;

namespace GLAZOV.M.CONVERTER.Tests.Settings;

/// <summary>
/// Настройки: значения по умолчанию проходят проверку, неверные отвергаются сразу с понятным текстом,
/// а изменение исходного объекта после создания конвертера на него не влияет.
/// </summary>
public sealed class ConverterSettingsSnapshotTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "convtests-" + Guid.NewGuid().ToString("N"));

    public ConverterSettingsSnapshotTests()
    {
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public void Defaults_PassValidation()
    {
        ConverterSettingsSnapshot snapshot = ConverterSettingsSnapshot.Create(new ConverterSettings());

        Assert.Equal(Environment.ProcessorCount, snapshot.MaxParallelTasks);
        Assert.Null(snapshot.TempFolder);
        Assert.Empty(snapshot.ToolPaths);
        Assert.False(snapshot.OverwriteExisting);
        Assert.Equal(64 * 1024, snapshot.DetectionWindow);
    }

    [Fact]
    public void FilledSettings_AreCopiedAsGiven()
    {
        ConverterSettings settings = new()
        {
            MaxParallelTasks = 64,
            TempFolder = _folder,
            OverwriteExisting = true,
            ToolPaths = { ["утилита"] = "C:/tools/tool.exe" },
        };

        ConverterSettingsSnapshot snapshot = ConverterSettingsSnapshot.Create(settings);

        Assert.Equal(64, snapshot.MaxParallelTasks);
        Assert.Equal(_folder, snapshot.TempFolder);
        Assert.True(snapshot.OverwriteExisting);
        Assert.Equal("C:/tools/tool.exe", snapshot.ToolPaths["УТИЛИТА"]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void MaxParallelTasksBelowOne_IsRejectedWithClearText(int value)
    {
        ArgumentException ex = Assert.Throws<ArgumentException>(() =>
            ConverterSettingsSnapshot.Create(new ConverterSettings { MaxParallelTasks = value }));

        Assert.Contains("MaxParallelTasks", ex.Message);
        Assert.Contains("не меньше 1", ex.Message);
    }

    [Fact]
    public void MissingTempFolder_IsRejectedWithClearText()
    {
        string missing = Path.Combine(_folder, "нет-такой");

        ArgumentException ex = Assert.Throws<ArgumentException>(() =>
            ConverterSettingsSnapshot.Create(new ConverterSettings { TempFolder = missing }));

        Assert.Contains("TempFolder", ex.Message);
        Assert.Contains("не существует", ex.Message);
    }

    [Fact]
    public void EmptyTempFolder_MeansSystemFolder()
    {
        ConverterSettingsSnapshot snapshot = ConverterSettingsSnapshot.Create(new ConverterSettings { TempFolder = "  " });

        Assert.Null(snapshot.TempFolder);
    }

    [Fact]
    public void EmptyToolPath_IsRejectedWithToolName()
    {
        ConverterSettings settings = new() { ToolPaths = { ["утилита"] = " " } };

        ArgumentException ex = Assert.Throws<ArgumentException>(() => ConverterSettingsSnapshot.Create(settings));

        Assert.Contains("утилита", ex.Message);
        Assert.Contains("ToolPaths", ex.Message);
    }

    [Fact]
    public void ChangingTheOriginalAfterCreation_DoesNotChangeTheSnapshot()
    {
        ConverterSettings settings = new() { MaxParallelTasks = 4, ToolPaths = { ["утилита"] = "a.exe" } };
        ConverterSettingsSnapshot snapshot = ConverterSettingsSnapshot.Create(settings);

        settings.MaxParallelTasks = 99;
        settings.OverwriteExisting = true;
        settings.ToolPaths["утилита"] = "b.exe";
        settings.ToolPaths["other"] = "c.exe";

        Assert.Equal(4, snapshot.MaxParallelTasks);
        Assert.False(snapshot.OverwriteExisting);
        Assert.Equal("a.exe", snapshot.ToolPaths["утилита"]);
        Assert.False(snapshot.ToolPaths.ContainsKey("other"));
    }

    [Fact]
    public void Converter_RejectsBadSettingsAtCreation()
    {
        Assert.Throws<ArgumentException>(() => new Converter(new ConverterSettings { MaxParallelTasks = 0 }));
        Assert.Throws<ArgumentNullException>(() => new Converter(null!));
    }

    [Fact]
    public void Converter_AcceptsDefaultsAndFilledSettings()
    {
        Assert.NotNull(new Converter());
        Assert.NotNull(new Converter(new ConverterSettings { MaxParallelTasks = 8, TempFolder = _folder }));
    }
}
