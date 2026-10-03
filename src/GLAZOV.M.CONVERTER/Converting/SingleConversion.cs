using System.Diagnostics;
using GLAZOV.M.CONVERTER.Capabilities;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Detection;
using GLAZOV.M.CONVERTER.Engines;
using GLAZOV.M.CONVERTER.Errors;
using GLAZOV.M.CONVERTER.IO;
using GLAZOV.M.CONVERTER.Settings;
using GLAZOV.M.CONVERTER.Tooling;

namespace GLAZOV.M.CONVERTER.Converting;

/// <summary>
/// Конвертация одного входа: открыть вход, подтвердить тип по начальным байтам, подобрать движок и программу,
/// открыть выход, выполнить, завершить запись. При ошибке и отмене временное удаляется, результат не остаётся.
/// </summary>
internal sealed class SingleConversion
{
    private readonly ConverterSettingsSnapshot _settings;
    private readonly EngineRegistry _registry;
    private readonly ToolLocator _locator;

    internal SingleConversion(ConverterSettingsSnapshot settings, EngineRegistry registry, ToolLocator locator)
    {
        _settings = settings;
        _registry = registry;
        _locator = locator;
    }

    /// <summary>
    /// Выполняет конвертацию. Папка во входе — неверное использование (<see cref="ArgumentException"/>).
    /// </summary>
    /// <param name="source">Файл или поток.</param>
    /// <param name="target">Файл, поток или память и формат результата.</param>
    /// <param name="options">Дополнительные параметры.</param>
    /// <param name="progress">Приёмник хода: 0 перед запуском движка и 100 после записи результата.</param>
    /// <param name="cancellationToken">Отмена.</param>
    internal async Task<ConversionResult> RunAsync(
        Source source,
        Target target,
        ConversionOptions? options,
        IProgress<ConversionProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (source.Kind == SourceKind.Folder)
        {
            throw new ArgumentException("Папку нельзя конвертировать как один вход: для пакета используйте пакетную конвертацию.", nameof(source));
        }

        Stopwatch watch = Stopwatch.StartNew();
        await using InputHandle input = await InputReader.OpenAsync(source, cancellationToken).ConfigureAwait(false);

        FileFormat from = await ConfirmTypeAsync(input, cancellationToken).ConfigureAwait(false);
        IConversionEngine engine = ChooseEngine(from, target.FileFormat, options);
        ToolStatus tool = LocateTool(engine);

        await using OutputSink sink = await OutputSink.OpenAsync(target, input.Name, _settings, cancellationToken: cancellationToken).ConfigureAwait(false);

        progress?.Report(new ConversionProgress(0));
        EngineRequest request = new(input.Stream, sink.Stream, from, target.FileFormat, options, tool, _settings.TempFolder ?? Path.GetTempPath());
        await engine.ConvertAsync(request, cancellationToken).ConfigureAwait(false);
        SinkResult written = await sink.CommitAsync(cancellationToken).ConfigureAwait(false);
        progress?.Report(new ConversionProgress(100));

        watch.Stop();
        return new ConversionResult(true, from, target.FileFormat, watch.Elapsed, written.OutputPath, written.Bytes, written.Stream);
    }

    // Тип определяется по начальным байтам; имя файла — только подсказка для сообщения, доказательством не служит.
    private static async Task<FileFormat> ConfirmTypeAsync(InputHandle input, CancellationToken cancellationToken)
    {
        FileFormat from = await FileTypeDetector.DetectAsync(input, cancellationToken).ConfigureAwait(false);
        if (from.Equals(FileFormat.Unknown))
        {
            string extension = Path.GetExtension(input.Name).TrimStart('.').ToUpperInvariant();
            throw Failures.TypeNotConfirmed(input.Name, extension.Length == 0 ? null : extension, from);
        }

        return from;
    }

    private IConversionEngine ChooseEngine(FileFormat from, FileFormat to, ConversionOptions? options)
    {
        IConversionEngine engine = _registry.Find(from, to) ?? throw Failures.UnsupportedConversion(from, to);
        if (options is { Operations.Count: > 0 })
        {
            throw Failures.UnsupportedOperations(from, to);
        }

        return engine;
    }

    private ToolStatus LocateTool(IConversionEngine engine)
    {
        ToolStatus tool = _locator.Locate(engine.ToolName);
        return tool.Found ? tool : throw Failures.ToolNotFound(engine.ToolName, tool.InstallHint);
    }
}
