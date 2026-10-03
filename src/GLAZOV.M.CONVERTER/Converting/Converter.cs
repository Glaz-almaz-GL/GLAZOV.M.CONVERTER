using GLAZOV.M.CONVERTER.Batch;
using GLAZOV.M.CONVERTER.Capabilities;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Engines;
using GLAZOV.M.CONVERTER.Errors;
using GLAZOV.M.CONVERTER.Internal;
using GLAZOV.M.CONVERTER.Settings;
using GLAZOV.M.CONVERTER.Tooling;

namespace GLAZOV.M.CONVERTER.Converting;

/// <summary>
/// Главный тип библиотеки: конвертация файлов, потоков и папок между форматами.
/// </summary>
public sealed class Converter
{
    /// <summary>
    /// Создаёт конвертер с настройками по умолчанию.
    /// </summary>
    public Converter()
        : this(new ConverterSettings())
    {
    }

    /// <summary>
    /// Создаёт конвертер с заданными настройками.
    /// </summary>
    /// <param name="settings">Настройки; неверное значение отвергается сразу.</param>
    public Converter(ConverterSettings settings)
        : this(settings, EngineRegistry.CreateDefault(), null)
    {
    }

    internal Converter(ConverterSettings settings, EngineRegistry registry, ToolEnvironment? environment)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(registry);
        Settings = ConverterSettingsSnapshot.Create(settings);
        Capabilities = new ConverterCapabilities();
        _single = new SingleConversion(Settings, registry, new ToolLocator(Settings, environment));
        _batch = new BatchConversion(Settings, _single);
    }

    /// <summary>
    /// Что поддерживается и какие внешние программы найдены.
    /// </summary>
    public ConverterCapabilities Capabilities { get; }

    internal ConverterSettingsSnapshot Settings { get; }

    private readonly SingleConversion _single;

    private readonly BatchConversion _batch;

    /// <summary>
    /// Конвертирует один вход в один результат. При ошибке бросает <see cref="ConversionException"/>.
    /// </summary>
    /// <param name="source">Файл или поток.</param>
    /// <param name="target">Файл, поток или память — и формат результата.</param>
    /// <param name="options">Дополнительные параметры; пусто — без них.</param>
    /// <param name="progress">Приёмник хода выполнения.</param>
    /// <param name="cancellationToken">Отмена; бросает <see cref="OperationCanceledException"/>.</param>
    public Task<ConversionResult> ConvertAsync(
        Source source,
        Target target,
        ConversionOptions? options = null,
        IProgress<ConversionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        return _single.RunAsync(source, target, options, progress, cancellationToken);
    }

    /// <summary>
    /// Конвертирует папку или один пакетный источник. Из-за неудач отдельных входов не бросает — всё в отчёте.
    /// </summary>
    /// <param name="source">Папка с файлами.</param>
    /// <param name="target">Папка результатов и формат.</param>
    /// <param name="progress">Приёмник хода выполнения.</param>
    /// <param name="cancellationToken">Отмена.</param>
    public Task<BatchReport> ConvertBatchAsync(
        Source source,
        Target target,
        IProgress<BatchProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        return _batch.RunFolderAsync(source, target, progress, cancellationToken);
    }

    /// <summary>
    /// Конвертирует список входов. Из-за неудач отдельных входов не бросает — всё в отчёте.
    /// </summary>
    /// <param name="sources">Входы; пустой список — неверное использование.</param>
    /// <param name="target">Папка результатов и формат.</param>
    /// <param name="progress">Приёмник хода выполнения.</param>
    /// <param name="cancellationToken">Отмена.</param>
    public Task<BatchReport> ConvertBatchAsync(
        IEnumerable<Source> sources,
        Target target,
        IProgress<BatchProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(target);
        return _batch.RunListAsync(sources, target, progress, cancellationToken);
    }
}
