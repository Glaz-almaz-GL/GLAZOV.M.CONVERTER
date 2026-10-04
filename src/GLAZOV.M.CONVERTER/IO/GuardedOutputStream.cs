using GLAZOV.M.CONVERTER.Errors;

namespace GLAZOV.M.CONVERTER.IO;

/// <summary>
/// Wraps the stream an engine writes the result to: a write or flush that fails for lack of access, space or a broken
/// target becomes a <see cref="ConversionException"/> with <see cref="ConversionFailureReason.OutputUnwritable"/>.
/// Closing the wrapper does not close the stream it wraps unless told to.
/// </summary>
/// <param name="inner">The stream written to.</param>
/// <param name="name">Name of the result for the message: a file name or «поток».</param>
/// <param name="ownsInner">Whether disposing the wrapper disposes the wrapped stream.</param>
internal sealed class GuardedOutputStream(Stream inner, string name, bool ownsInner) : Stream
{
    /// <inheritdoc />
    public override bool CanRead => inner.CanRead;

    /// <inheritdoc />
    public override bool CanSeek => inner.CanSeek;

    /// <inheritdoc />
    public override bool CanWrite => inner.CanWrite;

    /// <inheritdoc />
    public override long Length => inner.Length;

    /// <inheritdoc />
    public override long Position
    {
        get => inner.Position;
        set => inner.Position = value;
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count)
    {
        return inner.Read(buffer, offset, count);
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin)
    {
        return inner.Seek(offset, origin);
    }

    /// <inheritdoc />
    public override void SetLength(long value)
    {
        Guard(() => inner.SetLength(value));
    }

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count)
    {
        Guard(() => inner.Write(buffer, offset, count));
    }

    /// <inheritdoc />
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        try
        {
            inner.Write(buffer);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw Failures.OutputUnwritable(name, ex, ConversionStage.Conversion);
        }
    }

    /// <inheritdoc />
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        try
        {
            await inner.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw Failures.OutputUnwritable(name, ex, ConversionStage.Conversion);
        }
    }

    /// <inheritdoc />
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    /// <inheritdoc />
    public override void Flush()
    {
        Guard(inner.Flush);
    }

    /// <inheritdoc />
    public override async Task FlushAsync(CancellationToken cancellationToken)
    {
        try
        {
            await inner.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw Failures.OutputUnwritable(name, ex, ConversionStage.Conversion);
        }
    }

    /// <inheritdoc />
    public override ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ownsInner ? inner.DisposeAsync() : ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && ownsInner)
        {
            inner.Dispose();
        }

        base.Dispose(disposing);
    }

    private void Guard(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw Failures.OutputUnwritable(name, ex, ConversionStage.Conversion);
        }
    }
}
