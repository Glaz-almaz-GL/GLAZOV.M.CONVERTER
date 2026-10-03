using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Errors;
using GLAZOV.M.CONVERTER.Settings;

namespace GLAZOV.M.CONVERTER.IO;

/// <summary>
/// Единая точка записи результата: файл, папка, поток или память. Пишется во временный файл (или в память)
/// и становится результатом только в <see cref="CommitAsync"/>; при ошибке и отмене временное удаляется.
/// </summary>
internal sealed class OutputSink : IAsyncDisposable
{
    private readonly Target _target;
    private readonly ConverterSettings _settings;
    private readonly string? _finalPath;
    private readonly string? _tempPath;
    private readonly Stream _stream;
    private readonly bool _ownsStream;
    private bool _finished;

    private OutputSink(Target target, ConverterSettings settings, string? finalPath, string? tempPath, Stream stream, bool ownsStream)
    {
        _target = target;
        _settings = settings;
        _finalPath = finalPath;
        _tempPath = tempPath;
        _stream = stream;
        _ownsStream = ownsStream;
    }

    /// <summary>
    /// Поток, в который пишет движок.
    /// </summary>
    internal Stream Stream => _stream;

    /// <summary>
    /// Умеет ли <see cref="Stream"/> перематываться (нужно форматам, которые правят заголовок после записи, например WAV).
    /// </summary>
    internal bool IsSeekable => _stream.CanSeek;

    /// <summary>
    /// Открывает приёмник. Существующий файл при <see cref="ConverterSettings.OverwriteExisting"/> = <c>false</c>
    /// отвергается сразу, до начала работы.
    /// </summary>
    /// <param name="target">Куда класть результат и в каком формате.</param>
    /// <param name="inputName">Имя входа — из него строится имя результата в папке.</param>
    /// <param name="settings">Настройки: перезапись, папка временных файлов.</param>
    /// <param name="requireSeekable">Нужен ли движку перематываемый вывод: для записываемого потока это даёт временный файл вместо канала.</param>
    /// <param name="cancellationToken">Отмена.</param>
    internal static ValueTask<OutputSink> OpenAsync(
        Target target,
        string inputName,
        ConverterSettings settings,
        bool requireSeekable = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(inputName);
        ArgumentNullException.ThrowIfNull(settings);
        cancellationToken.ThrowIfCancellationRequested();

        switch (target.Kind)
        {
            case TargetKind.File:
                return ValueTask.FromResult(OpenFile(target, settings, target.Path!));
            case TargetKind.Folder:
                var name = Path.GetFileNameWithoutExtension(inputName) + "." + target.Format.FileExtension();
                return ValueTask.FromResult(OpenFile(target, settings, Path.Combine(target.Path!, name)));
            case TargetKind.Memory:
                return ValueTask.FromResult(new OutputSink(target, settings, null, null, new MemoryStream(), ownsStream: true));
            default:
                return ValueTask.FromResult(OpenStream(target, settings, requireSeekable));
        }
    }

    /// <summary>
    /// Завершает запись: файл получает своё имя, память отдаёт байты и поток, чужой поток сбрасывается.
    /// </summary>
    /// <param name="cancellationToken">Отмена.</param>
    internal async ValueTask<SinkResult> CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_finished)
        {
            throw new InvalidOperationException("Запись уже завершена или отменена.");
        }

        try
        {
            await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);

            var result = _target.Kind switch
            {
                TargetKind.Memory => await CommitMemoryAsync().ConfigureAwait(false),
                TargetKind.Stream => await CommitStreamAsync(cancellationToken).ConfigureAwait(false),
                _ => await CommitFileAsync().ConfigureAwait(false),
            };

            _finished = true;
            return result;
        }
        catch
        {
            await AbortAsync().ConfigureAwait(false);
            throw;
        }
    }

    private async ValueTask<SinkResult> CommitMemoryAsync()
    {
        var bytes = ((MemoryStream)_stream).ToArray();
        await _stream.DisposeAsync().ConfigureAwait(false);
        return new SinkResult(null, bytes, new MemoryStream(bytes, writable: false));
    }

    // Чужой поток: если писали напрямую — только сбросили; если через временный файл — переливаем и удаляем его.
    private async ValueTask<SinkResult> CommitStreamAsync(CancellationToken cancellationToken)
    {
        if (_tempPath is not null)
        {
            _stream.Position = 0;
            await _stream.CopyToAsync(_target.OutputStream!, cancellationToken).ConfigureAwait(false);
            await _target.OutputStream!.FlushAsync(cancellationToken).ConfigureAwait(false);
            await _stream.DisposeAsync().ConfigureAwait(false);
            File.Delete(_tempPath);
        }

        return new SinkResult(null, null, null);
    }

    private async ValueTask<SinkResult> CommitFileAsync()
    {
        await _stream.DisposeAsync().ConfigureAwait(false);
        try
        {
            File.Move(_tempPath!, _finalPath!, overwrite: _settings.OverwriteExisting);
        }
        catch (IOException) when (!_settings.OverwriteExisting && File.Exists(_finalPath))
        {
            // Файл появился, пока шла работа: перезапись запрещена.
            throw FileExists(_finalPath!);
        }

        return new SinkResult(_finalPath, null, null);
    }

    /// <summary>
    /// Отменяет запись: недописанное удаляется, чужой поток не закрывается.
    /// </summary>
    internal async ValueTask AbortAsync()
    {
        if (_finished)
        {
            return;
        }

        _finished = true;
        if (_ownsStream)
        {
            await _stream.DisposeAsync().ConfigureAwait(false);
        }

        if (_tempPath is not null && File.Exists(_tempPath))
        {
            File.Delete(_tempPath);
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => AbortAsync();

    private static OutputSink OpenFile(Target target, ConverterSettings settings, string finalPath)
    {
        if (!settings.OverwriteExisting && File.Exists(finalPath))
        {
            throw FileExists(finalPath);
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(finalPath))!;
        Directory.CreateDirectory(directory);

        // Временный файл рядом с результатом: переименование внутри одной папки атомарно.
        var tempPath = Path.Combine(directory, $".{Path.GetFileName(finalPath)}.{Guid.NewGuid():N}.tmp");
        var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.Asynchronous);
        return new OutputSink(target, settings, finalPath, tempPath, stream, ownsStream: true);
    }

    private static OutputSink OpenStream(Target target, ConverterSettings settings, bool requireSeekable)
    {
        var output = target.OutputStream!;
        if (!output.CanWrite)
        {
            throw new ArgumentException("Поток результата не поддерживает запись.", nameof(target));
        }

        if (!requireSeekable)
        {
            return new OutputSink(target, settings, null, null, output, ownsStream: false);
        }

        // Движку нужен перематываемый вывод, а чужой поток может его не дать: пишем во временный файл
        // и при завершении переливаем в поток.
        var directory = settings.TempFolder ?? Path.GetTempPath();
        Directory.CreateDirectory(directory);
        var tempPath = Path.Combine(directory, $"convert-{Guid.NewGuid():N}.tmp");
        var temp = new FileStream(tempPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        return new OutputSink(target, settings, null, tempPath, temp, ownsStream: true);
    }

    private static ConversionException FileExists(string path) =>
        new(
            ConversionStage.Preparation,
            ConversionFailureReason.FileExists,
            $"Файл «{Path.GetFileName(path)}» уже существует; разрешите перезапись настройкой OverwriteExisting.");
}

/// <summary>
/// Итог записи: путь к файлу, либо байты и читаемый поток из памяти, либо ничего (записано в чужой поток).
/// </summary>
/// <param name="OutputPath">Путь к файлу результата.</param>
/// <param name="Bytes">Данные результата в памяти.</param>
/// <param name="Stream">Читаемый поток с результатом в памяти.</param>
internal sealed record SinkResult(string? OutputPath, byte[]? Bytes, Stream? Stream);
