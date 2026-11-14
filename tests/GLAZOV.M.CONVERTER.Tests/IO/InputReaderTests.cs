using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Errors;
using GLAZOV.M.CONVERTER.IO;
using GLAZOV.M.CONVERTER.Tests.Extension;

namespace GLAZOV.M.CONVERTER.Tests.IO;

/// <summary>
/// Чтение входа: файл, поток с перемоткой и без неё, папка; ошибки доступа — на настоящих файлах и намеренно плохих входах.
/// </summary>
public sealed class InputReaderTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "convtests-" + Guid.NewGuid().ToString("N"));

    public InputReaderTests()
    {
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
    }

    private string Put(string relative, byte[]? data = null)
    {
        string path = Path.Combine(_folder, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, data ?? [1, 2, 3]);
        return path;
    }

    private static async Task<byte[]> ReadAll(Stream stream)
    {
        using MemoryStream copy = new();
        await stream.CopyToAsync(copy);
        return copy.ToArray();
    }

    [Fact]
    public async Task OpenFile_NameIsFileName_ContentIsWhole()
    {
        byte[] expected = TestFormats.Sample(TestFormats.Beta, 200);
        string path = Put("a.wav", expected);

        await using var handle = await InputReader.OpenAsync(Source.File(path));

        Assert.Equal("a.wav", handle.Name);
        Assert.Equal(expected, await ReadAll(handle.Stream));
    }

    [Fact]
    public async Task OpenFile_HeadDoesNotConsumeBytes()
    {
        byte[] expected = TestFormats.Sample(TestFormats.Gamma, 200);
        string path = Put("b.ogg", expected);

        await using var handle = await InputReader.OpenAsync(Source.File(path));
        var head = await handle.HeadAsync(16);

        Assert.Equal(expected.AsSpan(0, 16).ToArray(), head.ToArray());
        Assert.Equal(expected, await ReadAll(handle.Stream));
    }

    [Fact]
    public async Task OpenFile_HeadLongerThanFile_ReturnsWhatThereIs()
    {
        string path = Put("short.bin", [9, 8, 7]);

        await using var handle = await InputReader.OpenAsync(Source.File(path));
        var head = await handle.HeadAsync(4096);

        Assert.Equal(new byte[] { 9, 8, 7 }, head.ToArray());
    }

    [Fact]
    public async Task SeekableStream_HeadReturnsPositionAndLeavesStreamOpen()
    {
        byte[] data = TestFormats.Sample(TestFormats.Beta, 200);
        MemoryStream source = new(data) { Position = 5 };

        await using (var handle = await InputReader.OpenAsync(Source.Stream(source)))
        {
            var head = await handle.HeadAsync(12);
            Assert.Equal(data.AsSpan(5, 12).ToArray(), head.ToArray());
            Assert.Equal(5, source.Position);
            Assert.Equal(data.AsSpan(5).ToArray(), await ReadAll(handle.Stream));
            Assert.Equal("stream", handle.Name);
        }

        Assert.True(source.CanRead);
    }

    [Fact]
    public async Task NonSeekableStream_HeadTwiceGivesSameBytes_AndReaderSeesEverything()
    {
        byte[] data = TestFormats.Sample(TestFormats.Alpha, 200);
        NonSeekableStream source = new(data);

        await using (var handle = await InputReader.OpenAsync(Source.Stream(source)))
        {
            byte[] first = (await handle.HeadAsync(32)).ToArray();
            byte[] second = (await handle.HeadAsync(32)).ToArray();
            byte[] longer = (await handle.HeadAsync(100)).ToArray();

            Assert.Equal(data.AsSpan(0, 32).ToArray(), first);
            Assert.Equal(first, second);
            Assert.Equal(data.AsSpan(0, 100).ToArray(), longer);
            Assert.Equal(data, await ReadAll(handle.Stream));
        }

        Assert.False(source.Disposed);
    }

    [Fact]
    public async Task NonSeekableStream_HeadAfterReadStarted_IsRefused()
    {
        NonSeekableStream source = new(TestFormats.Sample(TestFormats.Beta, 200));
        await using var handle = await InputReader.OpenAsync(Source.Stream(source));
        int read = await handle.Stream.ReadAsync(new byte[10]);
        Assert.True(read > 0);

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await handle.HeadAsync(500));
    }

    [Fact]
    public async Task MissingFile_IsInputUnreadableAtPreparation()
    {
        var ex = await Assert.ThrowsAsync<ConversionException>(async () =>
            await InputReader.OpenAsync(Source.File(Path.Combine(_folder, "нет-такого.mp3"))));

        Assert.Equal(ConversionStage.Preparation, ex.Stage);
        Assert.Equal(ConversionFailureReason.InputUnreadable, ex.Reason);
        Assert.Contains("нет-такого.mp3", ex.Message);
    }

    [Fact]
    public async Task FileLockedExclusively_IsInputUnreadable()
    {
        string path = Put("locked.mp3");
        await using FileStream holder = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var ex = await Assert.ThrowsAsync<ConversionException>(async () => await InputReader.OpenAsync(Source.File(path)));

        Assert.Equal(ConversionFailureReason.InputUnreadable, ex.Reason);
    }

    [Fact]
    public async Task FolderAsSingleInput_IsAUsageError()
    {
        await Assert.ThrowsAsync<ArgumentException>(async () => await InputReader.OpenAsync(Source.Folder(_folder)));
    }

    [Fact]
    public async Task StreamThatCannotRead_IsInputUnreadable()
    {
        var ex = await Assert.ThrowsAsync<ConversionException>(async () =>
            await InputReader.OpenAsync(Source.Stream(new WriteOnlyStream())));

        Assert.Equal(ConversionFailureReason.InputUnreadable, ex.Reason);
    }

    [Fact]
    public async Task Cancelled_BeforeOpen_ThrowsOperationCanceled()
    {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await InputReader.OpenAsync(Source.File(Put("c.mp3")), cts.Token));
    }

    [Fact]
    public void Expand_PatternAndRecursion_StableOrderByName()
    {
        Put("b.ogg");
        Put("a.ogg");
        Put("c.mp3");
        Put(Path.Combine("sub", "d.ogg"));

        string[] flat = InputReader.Expand(Source.Folder(_folder, "*.ogg", recursive: false)).Select(NameOf).ToArray();
        string[] deep = InputReader.Expand(Source.Folder(_folder, "*.ogg", recursive: true)).Select(NameOf).ToArray();
        string[] all = InputReader.Expand(Source.Folder(_folder, "*", recursive: true)).Select(NameOf).ToArray();

        Assert.Equal(["a.ogg", "b.ogg"], flat);
        Assert.Equal(["a.ogg", "b.ogg", Path.Combine("sub", "d.ogg")], deep);
        Assert.Equal(["a.ogg", "b.ogg", "c.mp3", Path.Combine("sub", "d.ogg")], all);
    }

    [Fact]
    public void Expand_EmptyFolder_IsEmptyList()
    {
        Assert.Empty(InputReader.Expand(Source.Folder(_folder)));
    }

    [Fact]
    public void Expand_MissingFolder_IsInputUnreadable()
    {
        var ex = Assert.Throws<ConversionException>(() =>
            InputReader.Expand(Source.Folder(Path.Combine(_folder, "нет-папки"))));

        Assert.Equal(ConversionFailureReason.InputUnreadable, ex.Reason);
    }

    [Fact]
    public void Expand_NotAFolder_IsAUsageError()
    {
        Assert.Throws<ArgumentException>(() => InputReader.Expand(Source.File(Put("x.mp3"))));
    }

    private string NameOf(Source source)
    {
        return Path.GetRelativePath(_folder, source.Path!);
    }

    private sealed class NonSeekableStream(byte[] data) : Stream
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
            return _inner.Read(buffer, offset, Math.Min(count, 7));
        }

        public override int Read(Span<byte> buffer)
        {
            return _inner.Read(buffer[..Math.Min(buffer.Length, 7)]);
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

    private sealed class WriteOnlyStream : Stream
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
        }
    }
}
