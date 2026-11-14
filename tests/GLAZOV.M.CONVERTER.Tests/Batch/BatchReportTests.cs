using GLAZOV.M.CONVERTER.Batch;
using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Errors;
using GLAZOV.M.CONVERTER.Tests.Extension;

namespace GLAZOV.M.CONVERTER.Tests.Batch;

/// <summary>
/// Unit tests for <see cref="BatchReport"/>: item counting, outcome consistency, and failed source ordering.
/// </summary>
public class BatchReportTests
{
    private static ConversionResult DummyResult(string? outputPath)
    {
        return new ConversionResult(true, TestFormats.Alpha, TestFormats.Beta, TimeSpan.Zero)
        {
            OutputPath = outputPath
        };
    }

    // 10 inputs: 0-5 succeeded, 6-8 failed, 9 canceled.
    private static (BatchReport Report, Source[] Sources) Ten()
    {
        Source[] sources = [.. Enumerable.Range(0, 10).Select(i => Source.File($"f{i}.alpha"))];
        List<BatchItemResult> items = [];
        for (int i = 0; i < 6; i++)
        {
            items.Add(BatchItemResult.Success($"f{i}.alpha", DummyResult($"out{i}.beta")));
        }

        items.Add(BatchItemResult.Failure("f6.alpha", Failures.TypeNotConfirmed("f6.alpha", "ALPHA", FileFormat.Unknown)));
        items.Add(BatchItemResult.Failure("f7.alpha", Failures.ToolFailed("faketool", 1, "broken")));
        items.Add(BatchItemResult.Failure("f8.alpha", Failures.FileExists("out8.beta")));
        items.Add(BatchItemResult.Cancelled("f9.alpha"));

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
        Assert.Equal("f2.alpha", item.SourceName);
        Assert.Equal("out2.beta", item.OutputPath);
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
        Assert.Contains("faketool", item.Message);
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
        Assert.Throws<ArgumentException>(() => new BatchReport([BatchItemResult.Success("a", DummyResult(null))], []));
        Assert.Throws<ArgumentNullException>(() => new BatchReport(null!, []));
        Assert.Throws<ArgumentNullException>(() => BatchItemResult.Failure("a", null!));
    }
}
