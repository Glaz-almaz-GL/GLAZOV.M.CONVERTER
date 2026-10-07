namespace GLAZOV.M.CONVERTER.ArchivesExtra.Reading;

/// <summary>
/// Reads another stream forward only, through its plain synchronous read: the decompressing streams of SharpCompress do not support everything
/// a TAR reader or a copy asks of a stream (seeking, the length, the asynchronous read of a memory block).
/// </summary>
/// <param name="inner">The decompressing stream.</param>
internal sealed class ForwardReadStream(Stream inner) : Stream
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
        try
        {
            return inner.Read(buffer, offset, count);
        }
        catch (Exception ex) when (ex is not (OperationCanceledException or OutOfMemoryException))
        {
            throw new InvalidDataException($"Файл повреждён или оборван: {ex.Message}", ex);
        }
    }

    public override int Read(Span<byte> buffer)
    {
        byte[] rented = System.Buffers.ArrayPool<byte>.Shared.Rent(buffer.Length);
        try
        {
            int read = Read(rented, 0, buffer.Length);
            rented.AsSpan(0, read).CopyTo(buffer);
            return read;
        }
        finally
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(rented);
        }
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Read(buffer.Span));
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
        if (disposing)
        {
            try
            {
                inner.Dispose();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // a decompressor of a cut file may fail while it closes; the failure was already told when it was read
            }
        }

        base.Dispose(disposing);
    }
}
