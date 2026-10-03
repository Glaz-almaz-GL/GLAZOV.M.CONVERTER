using GLAZOV.M.CONVERTER.Batch;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Errors;

namespace GLAZOV.M.CONVERTER.Tests.Batch;

/// <summary>
/// Отчёт пакета: подсчёт, согласованность, возврат неудачных входов по порядку — на записях, собранных фабриками.
/// </summary>
public class BatchReportTests
{
    // 10 входов: 0–5 удались, 6, 7, 8 — ошибки, 9 — отменён.
    private static (BatchReport Report, Source[] Sources) Ten()
    {
        Source[] sources = Enumerable.Range(0, 10).Select(i => Source.File($"f{i}.mp3")).ToArray();
        List<BatchItemResult> items = [];
        for (int i = 0; i < 6; i++)
        {
            items.Add(BatchItemResult.Success($"f{i}.mp3", $"out{i}.wav"));
        }

        items.Add(BatchItemResult.Failure("f6.mp3", Failures.TypeNotConfirmed("f6.mp3", "MP3", FileFormat.Unknown)));
        items.Add(BatchItemResult.Failure("f7.mp3", Failures.ToolFailed("ffmpeg", 1, "сломалось")));
        items.Add(BatchItemResult.Failure("f8.mp3", Failures.FileExists("out8.wav")));
        items.Add(BatchItemResult.Cancelled("f9.mp3"));

        return (new BatchReport(items, sources), sources);
    }

    [Fact]
    public void Counts_AreCorrect_AndTheirSumIsTotal()
    {
        BatchReport report = Ten().Report;

        Assert.Equal(10, report.Total);
        Assert.Equal(6, report.Succeeded);
        Assert.Equal(3, report.Failed);
        Assert.Equal(1, report.Canceled);
        Assert.Equal(report.Total, report.Succeeded + report.Failed + report.Canceled);
    }

    [Fact]
    public void FailedSources_AreExactlyTheFailedOnes_InInputOrder_WithoutTheCancelled()
    {
        (BatchReport report, Source[] sources) = Ten();

        IReadOnlyList<Source> failed = report.FailedSources();

        Assert.Equal(3, failed.Count);
        Assert.Same(sources[6], failed[0]);
        Assert.Same(sources[7], failed[1]);
        Assert.Same(sources[8], failed[2]);
    }

    [Fact]
    public void Success_KeepsNameAndOutputPath_WithoutErrorFields()
    {
        BatchItemResult item = Ten().Report.Items[2];

        Assert.True(item.Succeeded);
        Assert.False(item.Canceled);
        Assert.Equal("f2.mp3", item.SourceName);
        Assert.Equal("out2.wav", item.OutputPath);
        Assert.Null(item.Stage);
        Assert.Null(item.Reason);
        Assert.Null(item.Message);
    }

    [Fact]
    public void Failure_TakesStageReasonAndMessageFromTheError()
    {
        BatchItemResult item = Ten().Report.Items[7];

        Assert.False(item.Succeeded);
        Assert.False(item.Canceled);
        Assert.Equal(ConversionStage.Conversion, item.Stage);
        Assert.Equal(ConversionFailureReason.ToolFailed, item.Reason);
        Assert.Contains("ffmpeg", item.Message);
        Assert.Null(item.OutputPath);
    }

    [Fact]
    public void Cancelled_IsNeitherSuccessNorFailure()
    {
        BatchItemResult item = Ten().Report.Items[9];

        Assert.True(item.Canceled);
        Assert.False(item.Succeeded);
        Assert.Null(item.Reason);
    }

    [Fact]
    public void EmptyReport_HasZeroEverywhere_AndNoFailedSources()
    {
        BatchReport report = new([], []);

        Assert.Equal(0, report.Total);
        Assert.Equal(0, report.Succeeded);
        Assert.Equal(0, report.Failed);
        Assert.Equal(0, report.Canceled);
        Assert.Empty(report.FailedSources());
    }

    [Fact]
    public void ItemsAndSourcesOfDifferentCount_AreAUsageError()
    {
        Assert.Throws<ArgumentException>(() => new BatchReport([BatchItemResult.Success("a", null)], []));
        Assert.Throws<ArgumentNullException>(() => new BatchReport(null!, []));
        Assert.Throws<ArgumentNullException>(() => BatchItemResult.Failure("a", null!));
    }
}
