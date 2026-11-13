using GLAZOV.M.CONVERTER.Errors;
using GLAZOV.M.CONVERTER.Settings;

namespace GLAZOV.M.CONVERTER.IO;

/// <summary>
/// The result of one input that is several files in a target folder. The engine writes into a staging folder inside the target;
/// <see cref="Commit"/> moves the files out under the overwrite policy, all or none. Disposing without a commit removes everything written.
/// </summary>
internal sealed class MultiFileOutput : IDisposable
{
    private readonly string _folder;
    private readonly ConverterSettingsSnapshot _settings;
    private readonly List<string> _moved = [];
    private bool _committed;

    private MultiFileOutput(string folder, string staging, ConverterSettingsSnapshot settings)
    {
        _folder = folder;
        StagingFolder = staging;
        _settings = settings;
    }

    /// <summary>
    /// The folder the engine writes to; the engine sees it as the output folder of the request.
    /// </summary>
    internal string StagingFolder { get; }

    /// <summary>
    /// Creates the target folder and an empty staging folder inside it (same volume, so moves are cheap).
    /// </summary>
    /// <param name="folder">The target folder.</param>
    /// <param name="settings">Settings: the overwrite policy.</param>
    internal static MultiFileOutput Open(string folder, ConverterSettingsSnapshot settings)
    {
        try
        {
            string full = Path.GetFullPath(folder);
            Directory.CreateDirectory(full);
            string staging = Path.Combine(full, $".{Guid.NewGuid():N}.tmp");
            Directory.CreateDirectory(staging);
            return new MultiFileOutput(full, staging, settings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            throw Failures.OutputUnwritable(Path.GetFileName(folder.TrimEnd('\\', '/')), ex, ConversionStage.Preparation);
        }
    }

    /// <summary>
    /// Moves the written files into the target folder and returns their paths. A subfolder, no file at all, or a name already taken
    /// (when overwriting is off) fails the whole set; nothing is left behind.
    /// </summary>
    /// <param name="engineName">Name of the engine type, for the message.</param>
    internal IReadOnlyList<string> Commit(string engineName)
    {
        try
        {
            string[] entries = [.. Directory.EnumerateFileSystemEntries(StagingFolder).Order(StringComparer.Ordinal)];
            if (entries.Length == 0)
            {
                throw Failures.EngineFailed(engineName, new InvalidOperationException("The engine wrote no file."));
            }

            if (entries.Any(Directory.Exists))
            {
                throw Failures.EngineFailed(engineName, new InvalidOperationException("The engine wrote a subfolder; only files are allowed."));
            }

            List<(string Source, string Target)> plan = [.. entries.Select(entry => (entry, Path.Combine(_folder, Path.GetFileName(entry))))];
            foreach ((string _, string target) in plan)
            {
                if (!_settings.OverwriteExisting && File.Exists(target))
                {
                    throw Failures.FileExists(target);
                }
            }

            foreach ((string source, string target) in plan)
            {
                File.Move(source, target, overwrite: _settings.OverwriteExisting);
                _moved.Add(target);
            }

            _committed = true;
            return [.. plan.Select(item => item.Target)];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw Failures.OutputUnwritable(Path.GetFileName(_folder), ex, ConversionStage.Conversion);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_committed)
        {
            foreach (string path in _moved)
            {
                TryDelete(() => File.Delete(path));
            }
        }

        TryDelete(() => Directory.Delete(StagingFolder, recursive: true));
    }

    private static void TryDelete(Action delete)
    {
        try
        {
            delete();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The staging folder is a leftover only; there is nothing more to do.
        }
    }
}
