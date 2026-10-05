namespace GLAZOV.M.CONVERTER.IO;

/// <summary>
/// Read-only view of a seekable stream from a given offset on that counts the bytes read and refuses to read past a budget.
/// </summary>
internal sealed class BudgetedStream : Stream
{
    private readonly Stream _inner;
    private readonly long _start;
    private readonly long _limit;
    private long _spent;

    internal BudgetedStream(Stream inner, long start, long limit)
    {
        _inner = inner;
        _start = start;
        _limit = limit;
    }

    public override bool CanRead => true;

    public override bool CanSeek => true;

    public override bool CanWrite => false;

    public override long Length => Math.Max(0, _inner.Length - _start);

    public override long Position
    {
        get => _inner.Position - _start;
        set => Seek(value, SeekOrigin.Begin);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        return Read(buffer.AsSpan(offset, count));
    }

    public override int Read(Span<byte> buffer)
    {
        int read = _inner.Read(buffer);
        _spent += read;
        if (_spent > _limit)
        {
            throw new ProbeLimitException(_limit);
        }

        return read;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        long target = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => Position + offset,
            _ => Length + offset,
        };
        if (target < 0)
        {
            throw new IOException("Seek before the start of the input.");
        }

        _inner.Position = _start + target;
        return target;
    }

    public override void Flush()
    {
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
