using System.Text;
using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Engines;
using GLAZOV.M.CONVERTER.Extensions;
using GLAZOV.M.CONVERTER.Errors;
using GLAZOV.M.CONVERTER.Settings;
using GLAZOV.M.CONVERTER.Tooling;

namespace GLAZOV.M.CONVERTER.Tests.Converting;

/// <summary>
/// Конвертация одного входа на подставном движке: порядок шагов, тип по содержимому, ошибки до записи,
/// чистота после ошибок и отмены. Настоящий FFmpeg — в тестах движка; здесь движок управляем.
/// </summary>
public sealed class SingleConversionTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "convtests-" + Guid.NewGuid().ToString("N"));
    private readonly string _tempFolder;
    private readonly string _toolFile;

    public SingleConversionTests()
    {
        Directory.CreateDirectory(_folder);
        _tempFolder = Path.Combine(_folder, "tmp");
        Directory.CreateDirectory(_tempFolder);
        _toolFile = Path.Combine(_folder, OperatingSystem.IsWindows() ? "faketool.exe" : "faketool");
        File.WriteAllBytes(_toolFile, [0]);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(_toolFile, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        }
    }

    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
    }

    private static byte[] Sample(string name)
    {
        return File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Samples", name));
    }

    private string Put(string name, byte[] data)
    {
        string path = Path.Combine(_folder, name);
        File.WriteAllBytes(path, data);
        return path;
    }

    private Converter Make(FakeEngine engine, bool overwrite = false)
    {
        ConverterSettings settings = new()
        {
            TempFolder = _tempFolder,
            OverwriteExisting = overwrite,
            ToolPaths = { ["faketool"] = _toolFile },
        };
        return new Converter(settings, new EngineRegistry([engine]), null);
    }

    private string[] Leftovers()
    {
        return Directory.GetFiles(_folder, "*.tmp", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(_tempFolder))
            .ToArray();
    }

    [Fact]
    public async Task FileToFile_ReportsTypeFormatPathAndDuration_AndWritesTheEngineOutput()
    {
        FakeEngine engine = new();
        string input = Put("песня.mp3", Sample("sample-id3.mp3"));
        string output = Path.Combine(_folder, "out", "песня.wav");

        ConversionResult result = await Make(engine).ConvertAsync(Source.File(input), Target.File(output, FileFormat.Wav));

        Assert.True(result.Succeeded);
        Assert.Same(FileFormat.Mp3, result.SourceType);
        Assert.Same(FileFormat.Wav, result.TargetFormat);
        Assert.Equal(output, result.OutputPath);
        Assert.Null(result.Bytes);
        Assert.True(result.Duration >= TimeSpan.Zero);
        Assert.Equal(FakeEngine.Wrap(Sample("sample-id3.mp3")), await File.ReadAllBytesAsync(output));
        Assert.Empty(Leftovers());
    }

    [Fact]
    public async Task StreamToStream_WorksWithoutSeek_AndLeavesStreamsOpen()
    {
        FakeEngine engine = new();
        byte[] data = Sample("sample-id3.mp3");
        using NonSeekable input = new(data);
        using MemoryStream output = new();

        ConversionResult result = await Make(engine).ConvertAsync(Source.Stream(input), Target.Stream(output, FileFormat.Wav));

        Assert.True(result.Succeeded);
        Assert.Equal(FakeEngine.Wrap(data), output.ToArray());
        Assert.False(input.Disposed);
        Assert.True(output.CanWrite);
    }

    [Fact]
    public async Task FileToMemory_GivesBytesAndReadableStream()
    {
        FakeEngine engine = new();
        string input = Put("a.mp3", Sample("sample-raw.mp3"));

        ConversionResult result = await Make(engine).ConvertAsync(Source.File(input), Target.Memory(FileFormat.Wav));

        Assert.Equal(FakeEngine.Wrap(Sample("sample-raw.mp3")), result.Bytes);
        Assert.NotNull(result.Stream);
        Assert.Null(result.OutputPath);
    }

    [Fact]
    public async Task TheTypeIsDecidedByContent_NotByTheExtension()
    {
        FakeEngine engine = new();
        string lying = Put("это-ogg.ogg", Sample("sample-id3.mp3"));

        ConversionResult result = await Make(engine).ConvertAsync(Source.File(lying), Target.Memory(FileFormat.Wav));

        Assert.Same(FileFormat.Mp3, result.SourceType);
    }

    [Fact]
    public async Task OptionsAndSettings_ReachTheEngine()
    {
        FakeEngine engine = new();
        string input = Put("a.mp3", Sample("sample-id3.mp3"));
        AudioOptions audio = new() { Bitrate = 64_000 };

        await Make(engine).ConvertAsync(Source.File(input), Target.Memory(FileFormat.Wav), new ConversionOptions { Audio = audio });

        Assert.Same(audio, engine.Last!.Options!.Audio);
        Assert.Same(FileFormat.Wav, engine.Last.To);
        Assert.Equal(_tempFolder, engine.Last.TempFolder);
        Assert.Equal(_toolFile, engine.Last.Tool!.Path);
    }

    [Fact]
    public async Task Progress_ReportsZeroBeforeAndHundredAfter()
    {
        FakeEngine engine = new();
        string input = Put("a.mp3", Sample("sample-id3.mp3"));
        List<double> reports = [];

        await Make(engine).ConvertAsync(
            Source.File(input),
            Target.Memory(FileFormat.Wav),
            progress: new Inline<ConversionProgress>(p => reports.Add(p.Percent)));

        Assert.Equal([0d, 100d], reports);
    }

    [Theory]
    [InlineData("мусор.mp3", "МУСОР")]
    [InlineData("пусто.wav", "")]
    public async Task UnrecognizedContent_IsTypeNotConfirmed_NothingIsWritten(string name, string content)
    {
        FakeEngine engine = new();
        string input = Put(name, Encoding.UTF8.GetBytes(content));
        string output = Path.Combine(_folder, "out.wav");

        ConversionException ex = await Assert.ThrowsAsync<ConversionException>(async () =>
            await Make(engine).ConvertAsync(Source.File(input), Target.File(output, FileFormat.Wav)));

        Assert.Equal(ConversionStage.Detection, ex.Stage);
        Assert.Equal(ConversionFailureReason.TypeNotConfirmed, ex.Reason);
        Assert.Contains(name, ex.Message);
        Assert.Contains("тип не распознан", ex.Message);
        Assert.Equal(0, engine.Calls);
        Assert.False(File.Exists(output));
        Assert.Empty(Leftovers());
    }

    [Fact]
    public async Task ConversionTheEngineCannotDo_IsUnsupportedConversion()
    {
        FakeEngine engine = new();
        string wav = Put("a.wav", Sample("sample.wav"));

        ConversionException ex = await Assert.ThrowsAsync<ConversionException>(async () =>
            await Make(engine).ConvertAsync(Source.File(wav), Target.Memory(FileFormat.Wav)));

        Assert.Equal(ConversionStage.Preparation, ex.Stage);
        Assert.Equal(ConversionFailureReason.UnsupportedConversion, ex.Reason);
        Assert.Contains("WAV → WAV", ex.Message);
        Assert.Equal(0, engine.Calls);
    }

    [Fact]
    public async Task ImageOperations_AreNotSupportedYet_AndNothingIsWritten()
    {
        FakeEngine engine = new();
        string input = Put("a.mp3", Sample("sample-id3.mp3"));
        string output = Path.Combine(_folder, "o.wav");

        ConversionException ex = await Assert.ThrowsAsync<ConversionException>(async () =>
            await Make(engine).ConvertAsync(
                Source.File(input),
                Target.File(output, FileFormat.Wav),
                new ConversionOptions { Operations = { Operation.RotateRight } }));

        Assert.Equal(ConversionFailureReason.UnsupportedConversion, ex.Reason);
        Assert.Contains("Поворот и масштаб не применимы к звуку:", ex.Message);
        Assert.EndsWith("→ WAV.", ex.Message);
        Assert.False(File.Exists(output));
        Assert.Equal(0, engine.Calls);
    }

    [Fact]
    public async Task MissingTool_IsToolNotFoundWithHint_BeforeAnyOutputExists()
    {
        FakeEngine engine = new("нет-такой-программы");
        string input = Put("a.mp3", Sample("sample-id3.mp3"));
        string output = Path.Combine(_folder, "o.wav");

        ConversionException ex = await Assert.ThrowsAsync<ConversionException>(async () =>
            await Make(engine).ConvertAsync(Source.File(input), Target.File(output, FileFormat.Wav)));

        Assert.Equal(ConversionFailureReason.ToolNotFound, ex.Reason);
        Assert.Equal("нет-такой-программы", ex.ToolName);
        Assert.Contains("PATH", ex.Message);
        Assert.False(File.Exists(output));
        Assert.Empty(Leftovers());
    }

    [Fact]
    public async Task ExistingOutput_WithoutOverwrite_IsFileExists_AndTheEngineIsNotRun()
    {
        FakeEngine engine = new();
        string input = Put("a.mp3", Sample("sample-id3.mp3"));
        string output = Put("o.wav", [1, 2, 3]);

        ConversionException ex = await Assert.ThrowsAsync<ConversionException>(async () =>
            await Make(engine).ConvertAsync(Source.File(input), Target.File(output, FileFormat.Wav)));

        Assert.Equal(ConversionFailureReason.FileExists, ex.Reason);
        Assert.Equal(0, engine.Calls);
        Assert.Equal(new byte[] { 1, 2, 3 }, await File.ReadAllBytesAsync(output));
    }

    [Fact]
    public async Task EngineFailure_PropagatesAndLeavesNoResultAndNoTempFiles()
    {
        FakeEngine engine = new() { Fail = Failures.ToolFailed("faketool", 9, "что-то сломалось") };
        string input = Put("a.mp3", Sample("sample-id3.mp3"));
        string output = Path.Combine(_folder, "o.wav");

        ConversionException ex = await Assert.ThrowsAsync<ConversionException>(async () =>
            await Make(engine).ConvertAsync(Source.File(input), Target.File(output, FileFormat.Wav)));

        Assert.Equal(ConversionFailureReason.ToolFailed, ex.Reason);
        Assert.Equal(9, ex.ToolExitCode);
        Assert.False(File.Exists(output));
        Assert.Empty(Leftovers());
    }

    [Fact]
    public async Task Cancellation_ThrowsOperationCanceled_AndLeavesNothing()
    {
        FakeEngine engine = new() { WaitForCancel = true };
        string input = Put("a.mp3", Sample("sample-id3.mp3"));
        string output = Path.Combine(_folder, "o.wav");
        using CancellationTokenSource cts = new();

        Task<ConversionResult> run = Make(engine).ConvertAsync(Source.File(input), Target.File(output, FileFormat.Wav), cancellationToken: cts.Token);
        await engine.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await run);
        Assert.False(File.Exists(output));
        Assert.Empty(Leftovers());
    }

    [Fact]
    public async Task MissingInputFile_IsInputUnreadable()
    {
        FakeEngine engine = new();

        ConversionException ex = await Assert.ThrowsAsync<ConversionException>(async () =>
            await Make(engine).ConvertAsync(Source.File(Path.Combine(_folder, "нет.mp3")), Target.Memory(FileFormat.Wav)));

        Assert.Equal(ConversionFailureReason.InputUnreadable, ex.Reason);
    }

    [Fact]
    public async Task FolderAsInput_IsAUsageError()
    {
        FakeEngine engine = new();

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await Make(engine).ConvertAsync(Source.Folder(_folder), Target.Memory(FileFormat.Wav)));
    }

    [Fact]
    public async Task NullArguments_AreUsageErrors()
    {
        Converter converter = Make(new FakeEngine());

        await Assert.ThrowsAsync<ArgumentNullException>(async () => await converter.ConvertAsync(null!, Target.Memory(FileFormat.Wav)));
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await converter.ConvertAsync(Source.File("a"), null!));
    }

    // Подставной движок: умеет только MP3 → WAV; «результат» — вход, обёрнутый маркером; управляемые сбой и ожидание отмены.
    private sealed class FakeEngine(string toolName = "faketool") : IConversionEngine
    {
        public int Calls { get; private set; }

        public ConversionRequest? Last { get; private set; }

        public ConversionException? Fail { get; init; }

        public bool WaitForCancel { get; init; }

        public bool Operations { get; init; }

        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string ToolName { get; } = toolName;

        public bool SupportsOperations => Operations;

        public IReadOnlyList<ConversionPair> Pairs { get; } = [new(FileFormat.Mp3, FileFormat.Wav)];

        public static byte[] Wrap(byte[] input)
        {
            return [.. Encoding.ASCII.GetBytes("ДВИЖОК:"), .. input];
        }

        public bool CanConvert(FileFormat from, FileFormat to)
        {
            return from.Equals(FileFormat.Mp3) && to.Equals(FileFormat.Wav);
        }

        public async Task ConvertAsync(ConversionRequest request, CancellationToken cancellationToken)
        {
            Calls++;
            Last = request;
            Started.TrySetResult();

            using MemoryStream copy = new();
            await request.Input.CopyToAsync(copy, cancellationToken);
            await request.Output.WriteAsync(Wrap(copy.ToArray()), cancellationToken);

            if (WaitForCancel)
            {
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            }

            if (Fail is not null)
            {
                throw Fail;
            }
        }
    }

    private sealed class Inline<T>(Action<T> action) : IProgress<T>
    {
        public void Report(T value)
        {
            action(value);
        }
    }

    private sealed class NonSeekable(byte[] data) : Stream
    {
        private readonly MemoryStream _inner = new(data);

        public bool Disposed { get; private set; }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return _inner.Read(buffer, offset, Math.Min(count, 100));
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }
}
