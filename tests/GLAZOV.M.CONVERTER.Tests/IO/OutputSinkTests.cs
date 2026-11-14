using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Errors;
using GLAZOV.M.CONVERTER.IO;
using GLAZOV.M.CONVERTER.Settings;
using GLAZOV.M.CONVERTER.Tests.Extension;

namespace GLAZOV.M.CONVERTER.Tests.IO;

/// <summary>
/// Unit tests for <see cref="OutputSink"/>: file, folder, stream, and memory targets,
/// overwrite enforcement, atomic staging commits, and temporary file cleanup.
/// </summary>
public sealed class OutputSinkTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "convtests-" + Guid.NewGuid().ToString("N"));
    private readonly byte[] _payload = [1, 2, 3, 4, 5, 6, 7, 8];

    public OutputSinkTests()
    {
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
    }

    private ConverterSettingsSnapshot Settings(bool overwrite = false)
    {
        string temp = Path.Combine(_folder, "tmp");
        Directory.CreateDirectory(temp);
        return ConverterSettingsSnapshot.Create(new ConverterSettings { OverwriteExisting = overwrite, TempFolder = temp });
    }

    private string[] Leftovers()
    {
        return Directory.GetFiles(_folder, "*.tmp", SearchOption.AllDirectories);
    }

    private static ValueTask<ConversionResult> CompleteAsync(OutputSink sink, CancellationToken cancellationToken = default)
    {
        return sink.CompleteAsync(TestFormats.Alpha, TimeSpan.Zero, reencoded: true, notes: [], engineName: "TestEngine", cancellationToken);
    }

    [Fact]
    public async Task File_WrittenViaTempAndRenamedOnCommit()
    {
        string path = Path.Combine(_folder, "out", "a.alpha");

        await using OutputSink sink = await OutputSink.OpenAsync(Target.File(path, TestFormats.Alpha), "in.ogg", Settings());
        await sink.Stream.WriteAsync(_payload);
        Assert.False(File.Exists(path));
        ConversionResult result = await CompleteAsync(sink);

        Assert.Equal(path, result.OutputPath);
        Assert.Equal(_payload, await File.ReadAllBytesAsync(path));
        Assert.Empty(Leftovers());
    }

    [Fact]
    public async Task Folder_NameIsInputNameWithFormatExtension()
    {
        string folder = Path.Combine(_folder, "ready");

        await using OutputSink sink = await OutputSink.OpenAsync(Target.Folder(folder, TestFormats.Beta), "song.ogg", Settings());
        await sink.Stream.WriteAsync(_payload);
        ConversionResult result = await CompleteAsync(sink);

        Assert.Equal(Path.Combine(folder, "song.beta"), result.OutputPath);
        Assert.True(File.Exists(result.OutputPath));
    }

    [Fact]
    public async Task ExistingFile_WithoutOverwrite_IsFileExistsBeforeAnyWork()
    {
        string path = Path.Combine(_folder, "exists.alpha");
        await File.WriteAllBytesAsync(path, [9]);

        ConversionException ex = await Assert.ThrowsAsync<ConversionException>(async () =>
            await OutputSink.OpenAsync(Target.File(path, TestFormats.Alpha), "in.ogg", Settings()));

        Assert.Equal(ConversionStage.Preparation, ex.Stage);
        Assert.Equal(ConversionFailureReason.FileExists, ex.Reason);
        Assert.Equal("\t"u8.ToArray(), await File.ReadAllBytesAsync(path));
        Assert.Empty(Leftovers());
    }

    [Fact]
    public async Task ExistingFile_WithOverwrite_IsReplaced()
    {
        string path = Path.Combine(_folder, "exists.alpha");
        await File.WriteAllBytesAsync(path, [9]);

        await using OutputSink sink = await OutputSink.OpenAsync(Target.File(path, TestFormats.Alpha), "in.ogg", Settings(overwrite: true));
        await sink.Stream.WriteAsync(_payload);
        await CompleteAsync(sink);

        Assert.Equal(_payload, await File.ReadAllBytesAsync(path));
    }

    [Fact]
    public async Task FileAppearingDuringWork_WithoutOverwrite_IsFileExistsOnCommit_AndNothingIsLeft()
    {
        string path = Path.Combine(_folder, "race.alpha");
        OutputSink sink = await OutputSink.OpenAsync(Target.File(path, TestFormats.Alpha), "in.ogg", Settings());
        await sink.Stream.WriteAsync(_payload);
        await File.WriteAllBytesAsync(path, [7]);

        ConversionException ex = await Assert.ThrowsAsync<ConversionException>(async () => await CompleteAsync(sink));

        Assert.Equal(ConversionFailureReason.FileExists, ex.Reason);
        Assert.Equal(new byte[] { 7 }, await File.ReadAllBytesAsync(path));
        Assert.Empty(Leftovers());
    }

    [Fact]
    public async Task Abort_RemovesTempFile_AndLeavesNoResult()
    {
        string path = Path.Combine(_folder, "aborted.alpha");
        OutputSink sink = await OutputSink.OpenAsync(Target.File(path, TestFormats.Alpha), "in.ogg", Settings());
        await sink.Stream.WriteAsync(_payload);
        Assert.Single(Leftovers());

        await sink.AbortAsync();

        Assert.False(File.Exists(path));
        Assert.Empty(Leftovers());
    }

    [Fact]
    public async Task DisposeWithoutCommit_IsAbort()
    {
        string path = Path.Combine(_folder, "forgotten.alpha");
        await using (OutputSink sink = await OutputSink.OpenAsync(Target.File(path, TestFormats.Alpha), "in.ogg", Settings()))
        {
            await sink.Stream.WriteAsync(_payload);
        }

        Assert.False(File.Exists(path));
        Assert.Empty(Leftovers());
    }

    [Fact]
    public async Task Memory_AfterCommit_GivesBytesAndReadableStream()
    {
        await using OutputSink sink = await OutputSink.OpenAsync(Target.Memory(TestFormats.Alpha), "in.ogg", Settings());
        await sink.Stream.WriteAsync(_payload);

        ConversionResult result = await CompleteAsync(sink);

        Assert.Null(result.OutputPath);
        Assert.Equal(_payload, result.Bytes);
        Assert.NotNull(result.Stream);
        Assert.Equal(0, result.Stream.Position);
        Assert.Equal(_payload.Length, result.Stream.Length);
    }

    [Fact]
    public async Task Stream_WithoutSeekRequirement_WritesDirectlyAndDoesNotCloseIt()
    {
        MemoryStream destination = new();

        await using (OutputSink sink = await OutputSink.OpenAsync(Target.Stream(destination, TestFormats.Gamma), "in.wav", Settings()))
        {
            Assert.NotSame(destination, sink.Stream);
            await sink.Stream.WriteAsync(_payload);
            ConversionResult result = await CompleteAsync(sink);
            Assert.Null(result.OutputPath);
        }

        Assert.True(destination.CanWrite);
        Assert.Equal(_payload, destination.ToArray());
    }

    [Fact]
    public async Task Stream_WithSeekRequirement_GoesThroughTempFile_AndCopiesOnCommit()
    {
        NonSeekableWriteStream destination = new();

        await using (OutputSink sink = await OutputSink.OpenAsync(Target.Stream(destination, TestFormats.Beta), "in.ogg", Settings(), requireSeekable: true))
        {
            Assert.True(sink.Stream.CanSeek);
            await sink.Stream.WriteAsync(_payload);
            sink.Stream.Position = 2;
            sink.Stream.WriteByte(99);
            Assert.Empty(destination.Written);
            await CompleteAsync(sink);
        }

        Assert.Equal(new byte[] { 1, 2, 99, 4, 5, 6, 7, 8 }, destination.Written);
        Assert.Empty(Leftovers());
    }

    [Fact]
    public async Task Stream_WithSeekRequirement_Abort_WritesNothingAndLeavesNoTemp()
    {
        NonSeekableWriteStream destination = new();
        OutputSink sink = await OutputSink.OpenAsync(Target.Stream(destination, TestFormats.Beta), "in.ogg", Settings(), requireSeekable: true);
        await sink.Stream.WriteAsync(_payload);

        await sink.AbortAsync();

        Assert.Empty(destination.Written);
        Assert.Empty(Leftovers());
    }

    [Fact]
    public async Task Cancelled_BeforeOpen_ThrowsOperationCanceled()
    {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await OutputSink.OpenAsync(Target.Memory(TestFormats.Alpha), "in.ogg", Settings(), cancellationToken: cts.Token));
    }

    [Fact]
    public async Task ReadOnlyOutputStream_IsAUsageError()
    {
        MemoryStream readOnly = new([], writable: false);

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await OutputSink.OpenAsync(Target.Stream(readOnly, TestFormats.Alpha), "in.ogg", Settings()));
    }

    [Fact]
    public async Task Folder_CustomFormat_NameUsesItsOwnExtension()
    {
        FileFormat custom = FileFormat.Create("CUSTOM", "application/x-test-custom", "cust");
        string folder = Path.Combine(_folder, "custom-out");

        await using OutputSink sink = await OutputSink.OpenAsync(Target.Folder(folder, custom), "song.wav", Settings());
        await sink.Stream.WriteAsync(_payload);
        ConversionResult result = await CompleteAsync(sink);

        Assert.Equal(Path.Combine(folder, "song.cust"), result.OutputPath);
    }

    private sealed class NonSeekableWriteStream : Stream
    {
        private readonly MemoryStream _written = new();

        public byte[] Written => _written.ToArray();

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
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
            _written.Write(buffer, offset, count);
        }
    }
}