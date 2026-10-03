using System.Text;
using GLAZOV.M.CONVERTER.Batch;
using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Engines;
using GLAZOV.M.CONVERTER.Errors;
using GLAZOV.M.CONVERTER.Settings;

namespace GLAZOV.M.CONVERTER.Tests.Converting;

/// <summary>
/// Пакетная конвертация на управляемом движке: отчёт без исключений из-за отдельных входов, число одновременных задач
/// равно настройке (пик доказан счётчиком), отмена, дубли имён, повтор неудачных.
/// </summary>
public sealed class BatchConversionTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "convtests-" + Guid.NewGuid().ToString("N"));
    private readonly string _tool;

    public BatchConversionTests()
    {
        Directory.CreateDirectory(_folder);
        _tool = Path.Combine(_folder, OperatingSystem.IsWindows() ? "faketool.exe" : "faketool");
        File.WriteAllBytes(_tool, [0]);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(_tool, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        }
    }

    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
    }

    private static byte[] Mp3()
    {
        return File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Samples", "sample-id3.mp3"));
    }

    private string Input(string relative, byte[]? content = null)
    {
        string path = Path.Combine(_folder, "in", relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content ?? Mp3());
        return path;
    }

    private string OutFolder => Path.Combine(_folder, "out");

    private Converter Make(PeakEngine engine, int parallel = 4, bool overwrite = false)
    {
        string temp = Path.Combine(_folder, "tmp");
        Directory.CreateDirectory(temp);
        ConverterSettings settings = new()
        {
            MaxParallelTasks = parallel,
            OverwriteExisting = overwrite,
            TempFolder = temp,
            ToolPaths = { ["faketool"] = _tool },
        };
        return new Converter(settings, new EngineRegistry([engine]), null);
    }

    [Fact]
    public async Task TwentyInputsThreeBroken_GiveReport17To3_WithoutException()
    {
        PeakEngine engine = new();
        for (int i = 0; i < 20; i++)
        {
            Input($"f{i:00}.mp3", i is 4 or 9 or 15 ? Encoding.UTF8.GetBytes("не звук") : null);
        }

        BatchReport report = await Make(engine).ConvertBatchAsync(Source.Folder(Path.Combine(_folder, "in")), Target.Folder(OutFolder, FileFormat.Wav));

        Assert.Equal(20, report.Total);
        Assert.Equal(17, report.Succeeded);
        Assert.Equal(3, report.Failed);
        Assert.Equal(0, report.Canceled);
        Assert.Equal(17, Directory.GetFiles(OutFolder, "*.wav").Length);
        Assert.All(report.Items.Where(i => !i.Succeeded), i => Assert.Equal(ConversionFailureReason.TypeNotConfirmed, i.Reason));
        Assert.Equal(["f04.mp3", "f09.mp3", "f15.mp3"], report.Items.Where(i => !i.Succeeded).Select(i => i.SourceName));
    }

    [Fact]
    public async Task RetryByFailedSources_ProcessesOnlyTheFailedOnes()
    {
        PeakEngine engine = new();
        Converter converter = Make(engine);
        for (int i = 0; i < 6; i++)
        {
            Input($"f{i}.mp3", i is 1 or 3 ? [7, 7, 7] : null);
        }

        BatchReport first = await converter.ConvertBatchAsync(Source.Folder(Path.Combine(_folder, "in")), Target.Folder(OutFolder, FileFormat.Wav));
        int callsAfterFirst = engine.Calls;
        BatchReport retry = await converter.ConvertBatchAsync(first.FailedSources(), Target.Folder(OutFolder, FileFormat.Wav));

        Assert.Equal(4, callsAfterFirst);
        Assert.Equal(4, engine.Calls);
        Assert.Equal(2, retry.Total);
        Assert.Equal(2, retry.Failed);
    }

    [Fact]
    public async Task ParallelTasks_EqualTheSetting_PeakIsExactlyThat()
    {
        PeakEngine engine = new() { GatherAt = 4 };
        for (int i = 0; i < 12; i++)
        {
            Input($"f{i}.mp3");
        }

        BatchReport report = await Make(engine, parallel: 4).ConvertBatchAsync(Source.Folder(Path.Combine(_folder, "in")), Target.Folder(OutFolder, FileFormat.Wav));

        Assert.Equal(12, report.Succeeded);
        Assert.Equal(4, engine.Peak);
    }

    [Fact]
    public async Task ParallelTasks_AboveTheOldCeiling_AreNotCapped()
    {
        PeakEngine engine = new() { GatherAt = 12 };
        for (int i = 0; i < 12; i++)
        {
            Input($"f{i}.mp3");
        }

        BatchReport report = await Make(engine, parallel: 64).ConvertBatchAsync(Source.Folder(Path.Combine(_folder, "in")), Target.Folder(OutFolder, FileFormat.Wav));

        Assert.Equal(12, report.Succeeded);
        Assert.Equal(12, engine.Peak);
    }

    [Fact]
    public async Task EmptyFolder_IsAnEmptyReport_NotAnError()
    {
        Directory.CreateDirectory(Path.Combine(_folder, "in"));

        BatchReport report = await Make(new PeakEngine()).ConvertBatchAsync(Source.Folder(Path.Combine(_folder, "in")), Target.Folder(OutFolder, FileFormat.Wav));

        Assert.Equal(0, report.Total);
        Assert.Empty(report.FailedSources());
    }

    [Fact]
    public async Task MissingFolder_IsInputUnreadable()
    {
        ConversionException ex = await Assert.ThrowsAsync<ConversionException>(async () =>
            await Make(new PeakEngine()).ConvertBatchAsync(Source.Folder(Path.Combine(_folder, "нет-папки")), Target.Folder(OutFolder, FileFormat.Wav)));

        Assert.Equal(ConversionFailureReason.InputUnreadable, ex.Reason);
    }

    [Fact]
    public async Task UsageErrors_AreExceptionsRightAway()
    {
        Converter converter = Make(new PeakEngine());
        string file = Input("a.mp3");

        await Assert.ThrowsAsync<ArgumentException>(async () => await converter.ConvertBatchAsync(Array.Empty<Source>(), Target.Folder(OutFolder, FileFormat.Wav)));
        await Assert.ThrowsAsync<ArgumentException>(async () => await converter.ConvertBatchAsync([Source.Folder(_folder)], Target.Folder(OutFolder, FileFormat.Wav)));
        await Assert.ThrowsAsync<ArgumentException>(async () => await converter.ConvertBatchAsync([Source.File(file)], Target.Memory(FileFormat.Wav)));
        await Assert.ThrowsAsync<ArgumentException>(async () => await converter.ConvertBatchAsync(Source.File(file), Target.Folder(OutFolder, FileFormat.Wav)));
        await Assert.ThrowsAsync<ArgumentException>(async () => await converter.ConvertBatchAsync(Source.Folder(_folder), Target.File("x.wav", FileFormat.Wav)));
    }

    [Fact]
    public async Task TwoInputsWithTheSameResultName_SecondIsRefusedDeterministically()
    {
        PeakEngine engine = new();
        string first = Input(Path.Combine("a", "song.mp3"));
        string second = Input(Path.Combine("b", "song.mp3"));

        BatchReport report = await Make(engine).ConvertBatchAsync([Source.File(first), Source.File(second)], Target.Folder(OutFolder, FileFormat.Wav));

        Assert.True(report.Items[0].Succeeded);
        Assert.False(report.Items[1].Succeeded);
        Assert.Equal(ConversionFailureReason.FileExists, report.Items[1].Reason);
        Assert.Contains("уже занят", report.Items[1].Message);
        Assert.Single(Directory.GetFiles(OutFolder, "*.wav"));
    }

    [Fact]
    public async Task ExistingResult_WithoutOverwrite_FailsOnlyThatInput()
    {
        PeakEngine engine = new();
        string a = Input("a.mp3");
        string b = Input("b.mp3");
        Directory.CreateDirectory(OutFolder);
        File.WriteAllBytes(Path.Combine(OutFolder, "a.wav"), [1]);

        BatchReport report = await Make(engine).ConvertBatchAsync([Source.File(a), Source.File(b)], Target.Folder(OutFolder, FileFormat.Wav));

        Assert.False(report.Items[0].Succeeded);
        Assert.Equal(ConversionFailureReason.FileExists, report.Items[0].Reason);
        Assert.True(report.Items[1].Succeeded);
        Assert.Equal(new byte[] { 1 }, await File.ReadAllBytesAsync(Path.Combine(OutFolder, "a.wav")));
    }

    [Fact]
    public async Task Streams_InAList_ConvertAndTakeTheNameStream()
    {
        PeakEngine engine = new();
        using MemoryStream stream = new(Mp3());

        BatchReport report = await Make(engine).ConvertBatchAsync([Source.Stream(stream)], Target.Folder(OutFolder, FileFormat.Wav));

        Assert.Equal(1, report.Succeeded);
        Assert.Equal("поток", report.Items[0].SourceName);
        Assert.True(File.Exists(Path.Combine(OutFolder, "поток.wav")));
    }

    [Fact]
    public async Task CancelMidway_GivesReportWithCancelledItems_AndNoException()
    {
        PeakEngine engine = new() { WaitForCancel = true };
        for (int i = 0; i < 8; i++)
        {
            Input($"f{i}.mp3");
        }

        using CancellationTokenSource cts = new();
        Task<BatchReport> run = Make(engine, parallel: 2).ConvertBatchAsync(
            Source.Folder(Path.Combine(_folder, "in")),
            Target.Folder(OutFolder, FileFormat.Wav),
            cancellationToken: cts.Token);
        await engine.TwoStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await cts.CancelAsync();
        BatchReport report = await run;

        Assert.Equal(8, report.Total);
        Assert.Equal(8, report.Canceled);
        Assert.Equal(0, report.Succeeded);
        Assert.Equal(2, engine.Calls);
        Assert.Empty(Directory.Exists(OutFolder) ? Directory.GetFiles(OutFolder) : []);
    }

    [Fact]
    public async Task CancelledBeforeStart_ThrowsOperationCanceled()
    {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();
        Input("a.mp3");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await Make(new PeakEngine()).ConvertBatchAsync(Source.Folder(Path.Combine(_folder, "in")), Target.Folder(OutFolder, FileFormat.Wav), cancellationToken: cts.Token));
    }

    [Fact]
    public async Task Progress_CountsToTotal_WithNames()
    {
        PeakEngine engine = new();
        for (int i = 0; i < 5; i++)
        {
            Input($"f{i}.mp3");
        }

        List<BatchProgress> reports = [];
        await Make(engine).ConvertBatchAsync(
            Source.Folder(Path.Combine(_folder, "in")),
            Target.Folder(OutFolder, FileFormat.Wav),
            new SyncProgress(reports));

        Assert.Equal(5, reports.Count);
        Assert.All(reports, r => Assert.Equal(5, r.Total));
        Assert.Equal(5, reports.Max(r => r.Completed));
        Assert.Equal(["f0.mp3", "f1.mp3", "f2.mp3", "f3.mp3", "f4.mp3"], reports.Select(r => r.CurrentName).Order());
    }

    [Fact]
    public async Task UnexpectedFailure_IsNotDisguisedAsAFailedInput()
    {
        PeakEngine engine = new() { Crash = new InvalidOperationException("непредвиденное") };
        Input("a.mp3");

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Make(engine).ConvertBatchAsync(Source.Folder(Path.Combine(_folder, "in")), Target.Folder(OutFolder, FileFormat.Wav)));

        Assert.Equal("непредвиденное", ex.Message);
    }

    private sealed class SyncProgress(List<BatchProgress> sink) : IProgress<BatchProgress>
    {
        public void Report(BatchProgress value)
        {
            lock (sink)
            {
                sink.Add(value);
            }
        }
    }

    // Управляемый движок MP3 → WAV: считает одновременные запуски, может ждать набора нужного числа или отмены.
    private sealed class PeakEngine : IConversionEngine
    {
        private int _running;
        private int _peak;
        private int _calls;
        private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int GatherAt { get; init; }

        public bool WaitForCancel { get; init; }

        public Exception? Crash { get; init; }

        public int Peak => Volatile.Read(ref _peak);

        public int Calls => Volatile.Read(ref _calls);

        public TaskCompletionSource TwoStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string ToolName => "faketool";

        public IReadOnlyList<(FileFormat From, FileFormat To)> Pairs { get; } = [(FileFormat.Mp3, FileFormat.Wav)];

        public bool CanConvert(FileFormat from, FileFormat to)
        {
            return from.Equals(FileFormat.Mp3) && to.Equals(FileFormat.Wav);
        }

        public async Task ConvertAsync(EngineRequest request, CancellationToken cancellationToken)
        {
            int calls = Interlocked.Increment(ref _calls);
            int now = Interlocked.Increment(ref _running);
            int seen;
            while (now > (seen = Volatile.Read(ref _peak)))
            {
                Interlocked.CompareExchange(ref _peak, now, seen);
            }

            if (calls >= 2)
            {
                TwoStarted.TrySetResult();
            }

            try
            {
                if (Crash is not null)
                {
                    throw Crash;
                }

                if (GatherAt > 0)
                {
                    if (now >= GatherAt)
                    {
                        _gate.TrySetResult();
                    }

                    await _gate.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
                }

                if (WaitForCancel)
                {
                    await Task.Delay(TimeSpan.FromSeconds(60), cancellationToken);
                }

                await request.Input.CopyToAsync(request.Output, cancellationToken);
            }
            finally
            {
                Interlocked.Decrement(ref _running);
            }
        }
    }
}
