namespace GLAZOV.M.CONVERTER.Archives.Safety;

/// <summary>
/// Wraps an archive entry stream, normalizes decompressor read errors into <see cref="InvalidDataException"/>,
/// and reports unpacked byte counts to the bomb guard.
/// </summary>
/// <param name="inner">Underlying entry content stream.</param>
/// <param name="onRead">Callback invoked with the number of bytes read on each operation.</param>
internal sealed class GuardedReadStream(Stream inner, Action<int> onRead) : Stream
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
        int read;
        try
        {
            read = inner.Read(buffer, offset, count);
        }
        catch (Exception ex) when (IsBrokenInput(ex))
        {
            throw Broken(ex);
        }

        onRead(read);
        return read;
    }

    public override int Read(Span<byte> buffer)
    {
        int read;
        try
        {
            read = inner.Read(buffer);
        }
        catch (Exception ex) when (IsBrokenInput(ex))
        {
            throw Broken(ex);
        }

        onRead(read);
        return read;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        int read;
        try
        {
            read = await inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsBrokenInput(ex))
        {
            throw Broken(ex);
        }

        onRead(read);
        return read;
    }

    private static bool IsBrokenInput(Exception ex)
    {
        return ex is not (OperationCanceledException or OutOfMemoryException);
    }

    private static InvalidDataException Broken(Exception ex)
    {
        return new InvalidDataException($"Архив повреждён или оборван: {ex.Message}", ex);
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
