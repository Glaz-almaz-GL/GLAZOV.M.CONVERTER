using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Extensions;
using GLAZOV.M.CONVERTER.IO;
using GLAZOV.M.CONVERTER.Tests.Extension;

namespace GLAZOV.M.CONVERTER.Tests.IO;

/// <summary>
/// The helper that gives an engine the input as a file: the real file as is, or a temp copy removed afterwards.
/// </summary>
public sealed class InputFileTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "convtests-" + Guid.NewGuid().ToString("N"));

    public InputFileTests()
    {
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
    }

    private ConversionRequest Request(Stream input, string? inputPath = null)
    {
        return new ConversionRequest(input, new MemoryStream(), TestFormats.Beta, TestFormats.Beta, new ConversionOptions(), null, _folder, inputPath);
    }

    [Fact]
    public async Task WithAPath_ReturnsThePathItself_AndKeepsTheFileOnDispose()
    {
        string path = Path.Combine(_folder, "real.bin");
        File.WriteAllBytes(path, [1, 2, 3]);

        InputFile file = await InputFile.EnsureAsync(Request(new MemoryStream(), path), CancellationToken.None);
        await file.DisposeAsync();

        Assert.Equal(path, file.Path);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task WithoutAPath_CopiesTheStream_AndDeletesTheCopyAfterSuccess()
    {
        byte[] data = [.. Enumerable.Range(0, 10_000).Select(i => (byte)i)];

        InputFile file = await InputFile.EnsureAsync(Request(new MemoryStream(data)), CancellationToken.None);
        string path = file.Path;

        Assert.Equal(_folder, Path.GetDirectoryName(path));
        Assert.Equal(data, await File.ReadAllBytesAsync(path));

        await file.DisposeAsync();

        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task WhenCopyingFails_TheCopyIsDeleted()
    {
        await Assert.ThrowsAsync<IOException>(() => InputFile.EnsureAsync(Request(new FailingStream()), CancellationToken.None));

        Assert.Empty(Directory.GetFiles(_folder));
    }

    [Fact]
    public async Task WhenCanceled_TheCopyIsDeleted()
    {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => InputFile.EnsureAsync(Request(new MemoryStream([1, 2, 3])), cts.Token));

        Assert.Empty(Directory.GetFiles(_folder));
    }

    private sealed class FailingStream : Stream
    {
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
            throw new IOException("сбой чтения");
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
    }
}
