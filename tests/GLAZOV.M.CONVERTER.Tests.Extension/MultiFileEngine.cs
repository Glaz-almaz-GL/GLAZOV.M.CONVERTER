using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Extensions;

namespace GLAZOV.M.CONVERTER.Tests.Extension;

/// <summary>
/// A stand-in engine whose pair writes several files: with a folder target it puts <see cref="Files"/> files named after the input
/// into <see cref="ConversionRequest.OutputFolder"/>; otherwise it writes the first one to the output stream and adds a note.
/// </summary>
public sealed class MultiFileEngine(FileFormat from, FileFormat to, int files = 3) : IConversionEngine
{
    public int Files { get; } = files;

    /// <summary>
    /// The engine throws after this many files are written; <c>-1</c> never.
    /// </summary>
    public int FailAfter { get; init; } = -1;

    /// <summary>
    /// Writes a subfolder as well, which the library must refuse.
    /// </summary>
    public bool WriteSubfolder { get; init; }

    public bool WaitForCancelAfterFirst { get; init; }

    /// <summary>
    /// Overrides the file name of a sheet (stem, number), to try names the library must refuse.
    /// </summary>
    public Func<string, int, string>? SheetName { get; init; }

    public string? FolderSeen { get; private set; }

    public bool SupportsOperations => false;

    public IReadOnlyList<ConversionPair> Pairs { get; } = [new ConversionPair(from, to) { WritesSeveralFiles = true }];

    public async Task ConvertAsync(ConversionRequest request, CancellationToken cancellationToken)
    {
        byte[] data = TestFormats.Sample(request.To, 8);
        if (request.OutputFolder is null)
        {
            await request.Output.WriteAsync(data, cancellationToken);
            request.AddNote($"book of {Files} sheets, sheet 1 taken");
            return;
        }

        FolderSeen = request.OutputFolder;
        string stem = request.InputPath is null ? "result" : Path.GetFileNameWithoutExtension(request.InputPath);
        for (int i = 1; i <= Files; i++)
        {
            if (FailAfter == i - 1)
            {
                throw new InvalidOperationException("broken after " + FailAfter);
            }

            await File.WriteAllBytesAsync(request.GetOutputFilePath(SheetName?.Invoke(stem, i) ?? $"{stem}-sheet{i}.{request.To.Extension}"), data, cancellationToken);
            if (i == 1 && WaitForCancelAfterFirst)
            {
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            }
        }

        if (WriteSubfolder)
        {
            Directory.CreateDirectory(Path.Combine(request.OutputFolder, "inner"));
        }
    }
}
