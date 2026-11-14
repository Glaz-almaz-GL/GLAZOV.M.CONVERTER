using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Errors;
using GLAZOV.M.CONVERTER.Settings;

namespace GLAZOV.M.CONVERTER.IO;

/// <summary>
/// Unified destination for a conversion result: a file, a folder (single or multi-file), a stream, or memory.
/// Writes to a staging location and atomically commits in <see cref="CompleteAsync"/>; aborts and cleans up on failure or cancellation.
/// </summary>
internal sealed class OutputSink : IAsyncDisposable
{
    private const string StreamName = "stream";
    private readonly Target _target;
    private readonly ConverterSettingsSnapshot _settings;
    private readonly string? _finalPath;
    private readonly string? _tempPath;
    private readonly bool _ownsStream;
    private readonly List<string> _movedFiles = [];
    private bool _finished;

    private OutputSink(
        Target target,
        ConverterSettingsSnapshot settings,
        string? finalPath,
        string? tempPath,
        string? stagingFolder,
        Stream stream,
        bool ownsStream)
    {
        _target = target;
        _settings = settings;
        _finalPath = finalPath;
        _tempPath = tempPath;
        StagingFolder = stagingFolder;
        Stream = stream;
        _ownsStream = ownsStream;
    }

    /// <summary>
    /// Writable stream for single-output conversions; <see cref="Stream.Null"/> when writing multiple files to <see cref="StagingFolder"/>.
    /// </summary>
    internal Stream Stream { get; }

    /// <summary>
    /// Staging folder path when the pair writes several files into a target folder; otherwise <c>null</c>.
    /// </summary>
    internal string? StagingFolder { get; }

    /// <summary>
    /// Opens a sink for the specified target. Rejects existing files immediately when <see cref="ConverterSettingsSnapshot.OverwriteExisting"/> is <c>false</c>.
    /// </summary>
    internal static ValueTask<OutputSink> OpenAsync(
        Target target,
        string inputName,
        ConverterSettingsSnapshot settings,
        bool writesSeveralFiles = false,
        bool requireSeekable = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(inputName);
        ArgumentNullException.ThrowIfNull(settings);
        cancellationToken.ThrowIfCancellationRequested();

        return target.Kind == TargetKind.Folder && writesSeveralFiles
            ? ValueTask.FromResult(OpenMultiFileFolder(target, settings))
            : ValueTask.FromResult(target.Kind switch
            {
                TargetKind.File => OpenFile(target, settings, target.Path!),
                TargetKind.Folder => OpenFile(target, settings, Path.Combine(target.Path!, $"{Path.GetFileNameWithoutExtension(inputName)}.{target.FileFormat.Extension}")),
                TargetKind.Memory => new OutputSink(target, settings, null, null, null, new MemoryStream(), ownsStream: true),
                _ => OpenStream(target, settings, requireSeekable),
            });
    }

    /// <summary>
    /// Finalizes the output and constructs the resulting <see cref="ConversionResult"/>.
    /// </summary>
    internal async ValueTask<ConversionResult> CompleteAsync(
        FileFormat sourceType,
        TimeSpan duration,
        bool reencoded,
        IReadOnlyList<string> notes,
        string engineName,
        CancellationToken cancellationToken = default)
    {
        if (_finished)
        {
            throw new InvalidOperationException("The output sink has already been completed or aborted.");
        }

        try
        {
            await Stream.FlushAsync(cancellationToken).ConfigureAwait(false);

            ConversionResult result = StagingFolder is not null
                ? CommitMultiFileFolder(sourceType, duration, reencoded, notes, engineName)
                : _target.Kind switch
                {
                    TargetKind.Memory => await CommitMemoryAsync(sourceType, duration, reencoded, notes).ConfigureAwait(false),
                    TargetKind.Stream => await CommitStreamAsync(sourceType, duration, reencoded, notes, cancellationToken).ConfigureAwait(false),
                    _ => await CommitFileAsync(sourceType, duration, reencoded, notes).ConfigureAwait(false),
                };

            _finished = true;
            return result;
        }
        catch (Exception ex) when (ex is (IOException or UnauthorizedAccessException) and not ConversionException)
        {
            await AbortAsync().ConfigureAwait(false);
            throw Failures.OutputUnwritable(NameOfResult(), ex, ConversionStage.Conversion);
        }
        catch
        {
            await AbortAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Cancels the write operation and deletes any temporary or partially moved files. External streams are left open.
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
            await Stream.DisposeAsync().ConfigureAwait(false);
        }

        foreach (string path in _movedFiles)
        {
            TryDelete(() => File.Delete(path));
        }

        if (_tempPath is not null)
        {
            TryDelete(() => File.Delete(_tempPath));
        }

        if (StagingFolder is not null)
        {
            TryDelete(() => Directory.Delete(StagingFolder, recursive: true));
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        return AbortAsync();
    }

    private async ValueTask<ConversionResult> CommitMemoryAsync(FileFormat sourceType, TimeSpan duration, bool reencoded, IReadOnlyList<string> notes)
    {
        byte[] bytes = ((MemoryStream)Stream).ToArray();
        await Stream.DisposeAsync().ConfigureAwait(false);
        return new ConversionResult(true, sourceType, _target.FileFormat, duration)
        {
            Bytes = bytes,
            Stream = new MemoryStream(bytes, writable: false),
            Reencoded = reencoded,
            Notes = notes,
        };
    }

    private async ValueTask<ConversionResult> CommitStreamAsync(FileFormat sourceType, TimeSpan duration, bool reencoded, IReadOnlyList<string> notes, CancellationToken cancellationToken)
    {
        if (_tempPath is not null)
        {
            Stream.Position = 0;
            await Stream.CopyToAsync(_target.OutputStream!, cancellationToken).ConfigureAwait(false);
            await _target.OutputStream!.FlushAsync(cancellationToken).ConfigureAwait(false);
            await Stream.DisposeAsync().ConfigureAwait(false);
            TryDelete(() => File.Delete(_tempPath));
        }

        return new ConversionResult(true, sourceType, _target.FileFormat, duration)
        {
            Reencoded = reencoded,
            Notes = notes,
        };
    }

    private async ValueTask<ConversionResult> CommitFileAsync(FileFormat sourceType, TimeSpan duration, bool reencoded, IReadOnlyList<string> notes)
    {
        await Stream.DisposeAsync().ConfigureAwait(false);
        try
        {
            File.Move(_tempPath!, _finalPath!, overwrite: _settings.OverwriteExisting);
        }
        catch (IOException) when (!_settings.OverwriteExisting && File.Exists(_finalPath))
        {
            throw Failures.FileExists(_finalPath);
        }

        return new ConversionResult(true, sourceType, _target.FileFormat, duration)
        {
            OutputPath = _finalPath,
            OutputPaths = _finalPath is null ? [] : [_finalPath],
            Reencoded = reencoded,
            Notes = notes,
        };
    }

    private ConversionResult CommitMultiFileFolder(FileFormat sourceType, TimeSpan duration, bool reencoded, IReadOnlyList<string> notes, string engineName)
    {
        string[] entries = [.. Directory.EnumerateFileSystemEntries(StagingFolder!).Order(StringComparer.Ordinal)];
        if (entries.Length == 0)
        {
            throw Failures.EngineFailed(engineName, new InvalidOperationException("The engine wrote no file."));
        }

        if (entries.Any(Directory.Exists))
        {
            throw Failures.EngineFailed(engineName, new InvalidOperationException("The engine wrote a subfolder; only files are allowed."));
        }

        string targetFolder = Path.GetFullPath(_target.Path!);
        List<(string Source, string Target)> plan = [.. entries.Select(entry => (entry, Path.Combine(targetFolder, Path.GetFileName(entry))))];

        foreach ((_, string destination) in plan)
        {
            if (!_settings.OverwriteExisting && File.Exists(destination))
            {
                throw Failures.FileExists(destination);
            }
        }

        foreach ((string source, string destination) in plan)
        {
            File.Move(source, destination, overwrite: _settings.OverwriteExisting);
            _movedFiles.Add(destination);
        }

        TryDelete(() => Directory.Delete(StagingFolder!, recursive: true));
        IReadOnlyList<string> paths = [.. plan.Select(p => p.Target)];
        return new ConversionResult(true, sourceType, _target.FileFormat, duration)
        {
            OutputPath = paths[0],
            OutputPaths = paths,
            Reencoded = reencoded,
            Notes = notes,
        };
    }

    private string NameOfResult()
    {
        if (_finalPath is not null)
        {
            return Path.GetFileName(_finalPath);
        }

        // Return StreamName if filename cannot be extracted
        return _target.Kind == TargetKind.Folder && _target.Path is not null
            ? Path.GetFileName(_target.Path.TrimEnd('\\', '/', ' '))
            : StreamName;
    }

    private static OutputSink OpenFile(Target target, ConverterSettingsSnapshot settings, string finalPath)
    {
        if (!settings.OverwriteExisting && File.Exists(finalPath))
        {
            throw Failures.FileExists(finalPath);
        }

        try
        {
            string directory = Path.GetDirectoryName(Path.GetFullPath(finalPath))!;
            Directory.CreateDirectory(directory);

            string tempPath = Path.Combine(directory, $".{Path.GetFileName(finalPath)}.{Guid.NewGuid():N}.tmp");
            FileStream stream = new(tempPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.Asynchronous);
            GuardedOutputStream guarded = new(stream, Path.GetFileName(finalPath), ownsInner: true);
            return new OutputSink(target, settings, finalPath, tempPath, null, guarded, ownsStream: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            throw Failures.OutputUnwritable(Path.GetFileName(finalPath), ex, ConversionStage.Preparation);
        }
    }
    private static OutputSink OpenMultiFileFolder(Target target, ConverterSettingsSnapshot settings)
    {
        try
        {
            string fullFolder = Path.GetFullPath(target.Path!);
            Directory.CreateDirectory(fullFolder);
            string staging = Path.Combine(fullFolder, $".{Guid.NewGuid():N}.tmp");
            Directory.CreateDirectory(staging);
            return new OutputSink(target, settings, null, null, staging, Stream.Null, ownsStream: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            throw Failures.OutputUnwritable(Path.GetFileName(target.Path!.TrimEnd('\\', '/', ' ')), ex, ConversionStage.Preparation);
        }
    }
    private static OutputSink OpenStream(Target target, ConverterSettingsSnapshot settings, bool requireSeekable)
    {
        Stream output = target.OutputStream!;
        if (!output.CanWrite)
        {
            throw new ArgumentException("The target stream does not support writing.", nameof(target));
        }
        if (!requireSeekable)
        {
            return new OutputSink(target, settings, null, null, null, new GuardedOutputStream(output, StreamName, ownsInner: false), ownsStream: false);
        }
        try
        {
            string directory = settings.TempFolder ?? Path.GetTempPath();
            Directory.CreateDirectory(directory);
            string tempPath = Path.Combine(directory, $"convert-{Guid.NewGuid():N}.tmp");
            FileStream temp = new(tempPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.DeleteOnClose);
            return new OutputSink(target, settings, null, tempPath, null, new GuardedOutputStream(temp, StreamName, ownsInner: true), ownsStream: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            throw Failures.OutputUnwritable(StreamName, ex, ConversionStage.Preparation);
        }
    }
    private static void TryDelete(Action delete)
    {
        try
        {
            delete();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best-effort cleanup of temporary artifacts.
        }
    }
}