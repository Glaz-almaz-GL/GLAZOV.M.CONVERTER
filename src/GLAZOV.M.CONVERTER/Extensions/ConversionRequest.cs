using GLAZOV.M.CONVERTER.Capabilities;
using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;

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
        Action<int>? onProgress = null)
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
    /// Найденная внешняя программа движка; <c>null</c>, если движок её не заявлял (<see cref="IConversionEngine.ToolName"/> пусто).
    /// </summary>
    public ToolStatus? Tool { get; }

    /// <summary>
    /// Существующая папка для временных файлов движка.
    /// </summary>
    public string TempFolder { get; }

    /// <summary>
    /// Path of the input file when the input is a file; <c>null</c> for a stream.
    /// </summary>
    public string? InputPath { get; }

    /// <summary>
    /// Reports engine progress, 0–100. Out-of-range values are clamped; repeated and smaller values are ignored.
    /// </summary>
    /// <param name="percent">Percent done.</param>
    public void ReportProgress(int percent)
    {
        _onProgress?.Invoke(percent);
    }
}
