using GLAZOV.M.CONVERTER.Data;

namespace GLAZOV.M.CONVERTER.Engines;

/// <summary>
/// Движок конвертации: умеет переводить входы одних форматов в другие. Ничего не знает о том,
/// откуда пришёл вход и куда пойдёт результат — только о потоках.
/// </summary>
internal interface IConversionEngine
{
    /// <summary>
    /// Имя внешней программы, которая нужна движку (например, <c>ffmpeg</c>).
    /// </summary>
    string ToolName { get; }

    /// <summary>
    /// Все пары «из какого формата в какой», которые умеет движок (для каталога возможностей).
    /// </summary>
    IReadOnlyList<(FileFormat From, FileFormat To)> Pairs { get; }

    /// <summary>
    /// Умеет ли движок эту конвертацию.
    /// </summary>
    /// <param name="from">Подтверждённый формат входа.</param>
    /// <param name="to">Формат результата.</param>
    bool CanConvert(FileFormat from, FileFormat to);

    /// <summary>
    /// Выполняет конвертацию. Ошибка внешней программы — <see cref="Errors.ConversionException"/>, отмена — <see cref="OperationCanceledException"/>.
    /// </summary>
    /// <param name="request">Вход, выход и параметры.</param>
    /// <param name="cancellationToken">Отмена.</param>
    Task ConvertAsync(EngineRequest request, CancellationToken cancellationToken);
}
