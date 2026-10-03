namespace GLAZOV.M.CONVERTER;

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
    {
        ArgumentNullException.ThrowIfNull(settings);
        Settings = settings;
        Capabilities = new ConverterCapabilities();
    }

    /// <summary>
    /// Что поддерживается и какие внешние программы найдены.
    /// </summary>
    public ConverterCapabilities Capabilities { get; }

    internal ConverterSettings Settings { get; }

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
        return Task.FromException<ConversionResult>(Stub.NotYet("Converter.ConvertAsync"));
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
        return Task.FromException<BatchReport>(Stub.NotYet("Converter.ConvertBatchAsync"));
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
        return Task.FromException<BatchReport>(Stub.NotYet("Converter.ConvertBatchAsync"));
    }
}
