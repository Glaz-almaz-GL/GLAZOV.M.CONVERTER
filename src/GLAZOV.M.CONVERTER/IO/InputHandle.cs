namespace GLAZOV.M.CONVERTER.IO;

/// <summary>
/// Открытый вход: имя для отчёта, читаемый поток и просмотр начальных байтов без их потери.
/// Чужой поток не закрывается при освобождении; файл, открытый библиотекой, закрывается.
/// </summary>
internal sealed class InputHandle : IAsyncDisposable
{
    private readonly Stream _inner;
    private readonly bool _ownsInner;
    private readonly bool _canRewind;
    private readonly long _startPosition;
    private readonly ReaderStream _reader;

    private byte[] _buffer = [];
    private int _bufferLength;
    private int _replayOffset;
    private bool _readStarted;

    internal InputHandle(string name, Stream inner, bool ownsInner)
    {
        Name = name;
        _inner = inner;
        _ownsInner = ownsInner;
        _canRewind = inner.CanSeek;
        _startPosition = _canRewind ? inner.Position : 0;
        _reader = new ReaderStream(this);
    }

    /// <summary>
    /// Имя входа для отчёта: имя файла или «поток».
    /// </summary>
    internal string Name { get; }

    /// <summary>
    /// Читаемый поток со всем содержимым входа, включая уже просмотренные начальные байты.
    /// </summary>
    internal Stream Stream => _reader;

    /// <summary>
    /// Возвращает первые байты входа, не потребляя их: после просмотра <see cref="Stream"/> читается с начала.
    /// Если вход короче, возвращается сколько есть.
    /// </summary>
    /// <param name="count">Сколько байтов нужно.</param>
    /// <param name="cancellationToken">Отмена.</param>
    internal ValueTask<ReadOnlyMemory<byte>> HeadAsync(int count, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        return _canRewind
            ? HeadFromSeekableAsync(count, cancellationToken)
            : HeadFromBufferedAsync(count, cancellationToken);
    }

    // Поток умеет искать: читаем и возвращаем позицию, где она была.
    private async ValueTask<ReadOnlyMemory<byte>> HeadFromSeekableAsync(int count, CancellationToken cancellationToken)
    {
        byte[] head = new byte[count];
        try
        {
            int total = await FillAsync(_inner, head, cancellationToken).ConfigureAwait(false);
            return head.AsMemory(0, total);
        }
        finally
        {
            _inner.Position = _startPosition;
        }
    }

    // Поток без перемотки: просмотренные байты запоминаются и отдаются читателю первыми.
    private async ValueTask<ReadOnlyMemory<byte>> HeadFromBufferedAsync(int count, CancellationToken cancellationToken)
    {
        if (count > _bufferLength)
        {
            if (_readStarted)
            {
                throw new InvalidOperationException("Просмотр начальных байтов невозможен после начала чтения входа без перемотки.");
            }

            if (_buffer.Length < count)
            {
                Array.Resize(ref _buffer, count);
            }

            _bufferLength += await FillAsync(_inner, _buffer.AsMemory(_bufferLength, count - _bufferLength), cancellationToken).ConfigureAwait(false);
        }

        return _buffer.AsMemory(0, Math.Min(count, _bufferLength));
    }

    // Читает, пока не заполнит буфер или не кончится вход; возвращает, сколько прочитано.
    private static async ValueTask<int> FillAsync(Stream stream, Memory<byte> destination, CancellationToken cancellationToken)
    {
        int total = 0;
        while (total < destination.Length)
        {
            int read = await stream.ReadAsync(destination[total..], cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return total;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _reader.DisposeAsync().ConfigureAwait(false);
        if (_ownsInner)
        {
            await _inner.DisposeAsync().ConfigureAwait(false);
        }
    }

    private int ReadCore(Span<byte> destination)
    {
        _readStarted = true;
        if (!_canRewind && _replayOffset < _bufferLength)
        {
            int count = Math.Min(destination.Length, _bufferLength - _replayOffset);
            _buffer.AsSpan(_replayOffset, count).CopyTo(destination);
            _replayOffset += count;
            return count;
        }

        return _inner.Read(destination);
    }

    private ValueTask<int> ReadCoreAsync(Memory<byte> destination, CancellationToken cancellationToken)
    {
        _readStarted = true;
        if (!_canRewind && _replayOffset < _bufferLength)
        {
            int count = Math.Min(destination.Length, _bufferLength - _replayOffset);
            _buffer.AsMemory(_replayOffset, count).CopyTo(destination);
            _replayOffset += count;
            return ValueTask.FromResult(count);
        }

        return _inner.ReadAsync(destination, cancellationToken);
    }

    /// <summary>
    /// Читатель: сначала отдаёт запомненные начальные байты, потом читает исходный поток. Сам ничего не закрывает.
    /// </summary>
    private sealed class ReaderStream(InputHandle owner) : Stream
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
            return owner.ReadCore(buffer.AsSpan(offset, count));
        }

        public override int Read(Span<byte> buffer)
        {
            return owner.ReadCore(buffer);
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return owner.ReadCoreAsync(buffer, cancellationToken);
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return owner.ReadCoreAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
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
