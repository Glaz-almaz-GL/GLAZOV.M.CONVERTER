namespace GLAZOV.M.CONVERTER.ArchivesExtra.Safety;

/// <summary>
/// Reads through the content of an entry and reports every chunk to the guard, which throws when a limit is gone over.
/// </summary>
/// <param name="inner">The content of the entry.</param>
/// <param name="onRead">Called with the number of bytes of every read.</param>
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

    // What the decompressing libraries throw on a cut or damaged input is told in plain words; cancellation passes as it is.
    // The limits are checked after the read, outside this guard, so they keep their own messages.
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
