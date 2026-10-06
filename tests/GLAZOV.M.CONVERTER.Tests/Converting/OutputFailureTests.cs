using GLAZOV.M.CONVERTER.Batch;
using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Errors;
using GLAZOV.M.CONVERTER.Extensions;
using GLAZOV.M.CONVERTER.Settings;
using GLAZOV.M.CONVERTER.Tests.Extension;

namespace GLAZOV.M.CONVERTER.Tests.Converting;

/// <summary>
/// A result that cannot be written is a ConversionException with the reason OutputUnwritable (file, folder, stream, batch),
/// with the file name only in the message, the system error inside, no temp files left, and cancellation still a cancellation.
/// The engine of the test extension copies the input to the output.
/// </summary>
public sealed class OutputFailureTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "convtests-" + Guid.NewGuid().ToString("N"));

    public OutputFailureTests()
    {
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        foreach (string file in Directory.EnumerateFiles(_folder, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(_folder, recursive: true);
    }

    private static Converter With(ConverterSettings? settings = null)
    {
        settings ??= new ConverterSettings();
        settings.Extensions.Add(TestExtensions.Create(new LambdaEngine(null, [new ConversionPair(TestFormats.Alpha, TestFormats.Beta)])));
        return new Converter(settings);
    }

    private string Input(string name = "вход.alpha")
    {
        string path = Path.Combine(_folder, name);
        File.WriteAllBytes(path, TestFormats.Sample(TestFormats.Alpha));
        return path;
    }

    [Fact]
    public async Task ParentThatIsAFile_CannotBeCreated_AtPreparation_WithTheFileNameOnly()
    {
        string blocker = Path.Combine(_folder, "файл-вместо-папки");
        File.WriteAllText(blocker, "x");
        string target = Path.Combine(blocker, "вложенная", "результат.beta");

        ConversionException ex = await Assert.ThrowsAsync<ConversionException>(() =>
            With().ConvertAsync(Source.File(Input()), Target.File(target, TestFormats.Beta)));

        Assert.Equal(ConversionFailureReason.OutputUnwritable, ex.Reason);
        Assert.Equal(ConversionStage.Preparation, ex.Stage);
        Assert.Contains("результат.beta", ex.Message);
        Assert.DoesNotContain(_folder, ex.Message);
        Assert.NotNull(ex.InnerException);
    }

    [Fact]
    public async Task ReadOnlyFileToOverwrite_IsOutputUnwritable_AtConversion_AndNoTempFileStays()
    {
        string target = Path.Combine(_folder, "готово.beta");
        File.WriteAllText(target, "старое");
        File.SetAttributes(target, FileAttributes.ReadOnly);
        Converter converter = With(new ConverterSettings { OverwriteExisting = true });

        ConversionException ex = await Assert.ThrowsAsync<ConversionException>(() =>
            converter.ConvertAsync(Source.File(Input()), Target.File(target, TestFormats.Beta)));

        Assert.Equal(ConversionFailureReason.OutputUnwritable, ex.Reason);
        Assert.Equal(ConversionStage.Conversion, ex.Stage);
        Assert.Contains("нет доступа", ex.Message);
        Assert.Equal("старое", File.ReadAllText(target));
        Assert.Empty(Directory.GetFiles(_folder, ".*.tmp"));
    }

    [Fact]
    public async Task InvalidPath_IsOutputUnwritable_OnWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string target = Path.Combine(_folder, "плохое|имя.beta");

        ConversionException ex = await Assert.ThrowsAsync<ConversionException>(() =>
            With().ConvertAsync(Source.File(Input()), Target.File(target, TestFormats.Beta)));

        Assert.Equal(ConversionFailureReason.OutputUnwritable, ex.Reason);
        Assert.Equal(ConversionStage.Preparation, ex.Stage);
    }

    [Fact]
    public async Task TargetStream_ThatFailsWhileWriting_IsOutputUnwritable_AtConversion()
    {
        await using FailingTarget target = new(new IOException("диск полон"));

        ConversionException ex = await Assert.ThrowsAsync<ConversionException>(() =>
            With().ConvertAsync(Source.File(Input()), Target.Stream(target, TestFormats.Beta)));

        Assert.Equal(ConversionFailureReason.OutputUnwritable, ex.Reason);
        Assert.Equal(ConversionStage.Conversion, ex.Stage);
        Assert.Contains("поток", ex.Message);
        Assert.IsType<IOException>(ex.InnerException);
    }

    [Fact]
    public async Task DiskFullErrorCode_IsNamedInPlainWords()
    {
        // ERROR_DISK_FULL (0x70) as the system reports it.
        IOException diskFull = new("нет места") { HResult = unchecked((int)0x80070070) };
        await using FailingTarget target = new(diskFull);

        ConversionException ex = await Assert.ThrowsAsync<ConversionException>(() =>
            With().ConvertAsync(Source.File(Input()), Target.Stream(target, TestFormats.Beta)));

        Assert.Contains("нет места на диске", ex.Message);
    }

    [Fact]
    public async Task LargeResultToAFailingStream_DoesNotHang_AndIsOutputUnwritable()
    {
        await using FailingTarget target = new(new IOException("диск полон"));
        string input = Path.Combine(_folder, "большой.alpha");
        File.WriteAllBytes(input, TestFormats.Sample(TestFormats.Alpha, 4 * 1024 * 1024));

        // The first write fails in the middle of a long copy.
        ConversionException ex = await Assert.ThrowsAsync<ConversionException>(() =>
            With().ConvertAsync(Source.File(input), Target.Stream(target, TestFormats.Beta)).WaitAsync(TimeSpan.FromSeconds(30)));

        Assert.Equal(ConversionFailureReason.OutputUnwritable, ex.Reason);
    }

    [Fact]
    public async Task Cancellation_StaysACancellation_NotAnOutputError()
    {
        await using FailingTarget target = new(new OperationCanceledException());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            With().ConvertAsync(Source.File(Input()), Target.Stream(target, TestFormats.Beta)));
    }

    [Fact]
    public async Task Batch_RecordsTheReason_ForTheBrokenItem_AndFinishesTheRest()
    {
        string folder = Path.Combine(_folder, "вход");
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, "а.alpha"), TestFormats.Sample(TestFormats.Alpha, 10));
        File.WriteAllBytes(Path.Combine(folder, "б.alpha"), TestFormats.Sample(TestFormats.Alpha, 20));
        string target = Path.Combine(_folder, "выход");
        Directory.CreateDirectory(target);
        string blocked = Path.Combine(target, "а.beta");
        File.WriteAllText(blocked, "старое");
        File.SetAttributes(blocked, FileAttributes.ReadOnly);
        Converter converter = With(new ConverterSettings { OverwriteExisting = true, MaxParallelTasks = 1 });

        BatchReport report = await converter.ConvertBatchAsync(Source.Folder(folder, "*.alpha"), Target.Folder(target, TestFormats.Beta));

        Assert.Equal(2, report.Total);
        Assert.Equal(1, report.Succeeded);
        BatchItemResult failed = Assert.Single(report.Items, i => !i.Succeeded);
        Assert.Equal("а.alpha", failed.SourceName);
        Assert.Equal(ConversionFailureReason.OutputUnwritable, failed.Reason);
        Assert.True(File.Exists(Path.Combine(target, "б.beta")));
    }

    // A stream that accepts writes and then refuses them with the given exception.
    private sealed class FailingTarget(Exception error) : Stream
    {
        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw error;
        }

        public override void Flush()
        {
            throw error;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }
    }
}
