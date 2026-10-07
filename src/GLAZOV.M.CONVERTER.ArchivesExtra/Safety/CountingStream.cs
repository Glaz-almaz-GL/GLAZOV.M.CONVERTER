namespace GLAZOV.M.CONVERTER.ArchivesExtra.Safety;

/// <summary>
/// Reads through another stream and counts the bytes that were read: the size of the input consumed so far, for the ratio and the progress.
/// </summary>
/// <param name="inner">The stream to read.</param>
internal sealed class CountingStream(Stream inner) : Stream
{
    internal long Count { get; private set; }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    // The XZ reader asks where it is in the input; the input is read from its start, so that is the count.
    public override long Position
    {
        get => Count;
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        int read = inner.Read(buffer, offset, count);
        Count += read;
        return read;
    }

    public override int Read(Span<byte> buffer)
    {
        int read = inner.Read(buffer);
        Count += read;
        return read;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        int read = await inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        Count += read;
        return read;
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
