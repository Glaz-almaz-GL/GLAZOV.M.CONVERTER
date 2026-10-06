using GLAZOV.M.CONVERTER.Capabilities;
using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.IO;

namespace GLAZOV.M.CONVERTER.Extensions;

/// <summary>
/// Что просят у движка: вход и выход потоками, подтверждённый формат входа и формат результата,
/// параметры вызова целиком, найденная программа движка и папка временных файлов.
/// </summary>
public sealed class ConversionRequest
{
    private readonly Action<int>? _onProgress;

    internal ConversionRequest(
        Stream input,
        Stream output,
        FileFormat from,
        FileFormat to,
        ConversionOptions options,
        ToolStatus? tool,
        string tempFolder,
        string? inputPath = null,
        Action<int>? onProgress = null,
        IReadOnlyDictionary<string, ToolStatus>? tools = null)
    {
        Input = input;
        Output = output;
        From = from;
        To = to;
        Options = options;
        Tool = tool;
        TempFolder = tempFolder;
        InputPath = inputPath;
        _onProgress = onProgress;
        Tools = tools ?? (tool is null ? new Dictionary<string, ToolStatus>() : new Dictionary<string, ToolStatus>(StringComparer.OrdinalIgnoreCase) { [tool.Name] = tool });
    }

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
    public IReadOnlyDictionary<string, ToolStatus> Tools { get; }

    /// <summary>
    /// Существующая папка для временных файлов движка.
    /// </summary>
    public string TempFolder { get; }

    /// <summary>
    /// Path of the input file when the input is a file; <c>null</c> for a stream.
    /// </summary>
    public string? InputPath { get; }

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
        _onProgress?.Invoke(percent);
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
