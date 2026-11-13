using GLAZOV.M.CONVERTER.Capabilities;
using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.IO;
using System.Collections.Frozen;

namespace GLAZOV.M.CONVERTER.Extensions;

/// <summary>
/// Что просят у движка: вход и выход потоками, подтверждённый формат входа и формат результата,
/// параметры вызова целиком, найденная программа движка и папка временных файлов.
/// </summary>
public sealed class ConversionRequest
{
    private readonly IReadOnlyDictionary<string, ToolStatus> _ownTools;
    private IReadOnlyDictionary<string, ToolStatus>? _tools;

    internal ConversionRequest(
        Stream input,
        Stream output,
        FileFormat from,
        FileFormat to,
        ConversionOptions options,
        ToolStatus? tool,
        string tempFolder)
    {
        Input = input;
        Output = output;
        From = from;
        To = to;
        Options = options;
        Tool = tool;
        TempFolder = tempFolder;

        // An engine with one program that is found: the dictionary holds just it.
        _ownTools = tool is null
            ? FrozenDictionary<string, ToolStatus>.Empty
            : new Dictionary<string, ToolStatus>(StringComparer.OrdinalIgnoreCase) { [tool.Name] = tool }.ToFrozenDictionary();
    }

    internal Action<int>? OnProgress { get; init; }

    /// <summary>
    /// Читаемый поток со всем входом, включая просмотренные при определении типа начальные байты.
    /// </summary>
    public Stream Input { get; }

    /// <summary>
    /// Поток, в который движок пишет результат. Библиотека закрывает его сама.
    /// </summary>
    public Stream Output { get; }

    /// <summary>
    /// Подтверждённый по начальным байтам формат входа.
    /// </summary>
    public FileFormat From { get; }

    /// <summary>
    /// Формат результата.
    /// </summary>
    public FileFormat To { get; }

    /// <summary>
    /// Параметры вызова целиком: звук, изображение, действия. Никогда не <c>null</c>.
    /// </summary>
    public ConversionOptions Options { get; }

    /// <summary>
    /// Первая обязательная внешняя программа движка (найдена всегда); <c>null</c>, если движок их не заявлял (<see cref="IConversionEngine.Tools"/> пусто).
    /// </summary>
    public ToolStatus? Tool { get; }

    /// <summary>
    /// Все программы движка по имени (без учёта регистра), в том числе необязательные: у такой <see cref="ToolStatus.Found"/> может быть <c>false</c>.
    /// </summary>
    public IReadOnlyDictionary<string, ToolStatus> Tools
    {
        get => _tools is { Count: > 0 } ? _tools : _ownTools;
        internal init => _tools = value;
    }

    /// <summary>
    /// Существующая папка для временных файлов движка.
    /// </summary>
    public string TempFolder { get; }

    /// <summary>
    /// Path of the input file when the input is a file; <c>null</c> for a stream.
    /// </summary>
    public string? InputPath { get; internal init; }

    /// <summary>
    /// Folder the engine writes the files of the result to; not <c>null</c> only for a pair that <see cref="ConversionPair.WritesSeveralFiles"/>
    /// converted to a folder target. <see cref="Output"/> is then an empty stream. Only files directly in this folder are taken as the result;
    /// the library moves them to the target folder under its overwrite policy, and on a failure or a cancellation removes them all.
    /// </summary>
    public string? OutputFolder { get; internal init; }

    internal List<string> Notes { get; } = [];

    /// <summary>
    /// The full path of a result file named <paramref name="fileName"/> inside <see cref="OutputFolder"/>; creates nothing and does not check
    /// whether the name is taken (the library does when it moves the files). An engine writes its result files only to paths from this method.
    /// </summary>
    /// <param name="fileName">A plain name: not empty, no folder separator, drive letter or "..", no character that any of the three systems refuses,
    /// no dot or space at the end, not a Windows device name (CON, NUL, COM1 and the like, with or without an extension), at most 255 characters.
    /// A name is refused, never cleaned: the engine decides how to name, say, a sheet that holds a bad character.</param>
    /// <exception cref="ArgumentException">The name is not allowed.</exception>
    /// <exception cref="InvalidOperationException">The pair does not write several files, so there is no <see cref="OutputFolder"/>.</exception>
    public string GetOutputFilePath(string fileName)
    {
        if (OutputFolder is null)
        {
            throw new InvalidOperationException("У этой пары нет папки результата: файлы пишутся только в Output.");
        }

        ResultFileName.Validate(fileName);
        return Path.Combine(OutputFolder, fileName);
    }

    /// <summary>
    /// Adds a line for the report of this input, for example that only the first sheet of a book was taken.
    /// </summary>
    /// <param name="note">The line, in plain words.</param>
    public void AddNote(string note)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(note);
        lock (Notes)
        {
            Notes.Add(note);
        }
    }

    /// <summary>
    /// Set by the engine: <c>false</c> when every track of the result was copied, not encoded. Starts as <c>true</c>.
    /// </summary>
    public bool Reencoded { get; set; } = true;

    /// <summary>
    /// Reports engine progress, 0–100. Out-of-range values are clamped; repeated and smaller values are ignored.
    /// </summary>
    /// <param name="percent">Percent done.</param>
    public void ReportProgress(int percent)
    {
        OnProgress?.Invoke(percent);
    }

    /// <summary>
    /// Gives the input as a file on disk, for programs that read a path: the request's own input file when there is one
    /// (it is not deleted), otherwise a copy of <see cref="Input"/> in <see cref="TempFolder"/>, deleted when the result is disposed,
    /// also after a failure or cancellation.
    /// </summary>
    /// <param name="cancellationToken">Cancellation of copying.</param>
    /// <returns>The file; dispose it after the program has finished.</returns>
    public Task<InputFile> EnsureInputFileAsync(CancellationToken cancellationToken = default)
    {
        return InputFile.EnsureAsync(this, cancellationToken);
    }
}
