using GLAZOV.M.CONVERTER.Data;

namespace GLAZOV.M.CONVERTER.Extensions;

/// <summary>
/// Определитель, которому для признака нужно читать вход дальше начала: архив, составной файл.
/// Спрашивается только на входе с перемоткой и только если метки ничего не назвали.
/// </summary>
public interface IStreamFormatDetector : IFormatDetector
{
    /// <summary>
    /// Стоит ли открывать вход: по началу видно, что он похож на тот контейнер, который определитель умеет разбирать.
    /// </summary>
    /// <param name="head">Начальные байты входа.</param>
    bool CanInspect(ReadOnlySpan<byte> head);

    /// <summary>
    /// Определяет формат, читая вход где угодно. Позиция 0 — начало входа; всего читать можно не больше 16 МБ.
    /// Превышение, ошибка ввода-вывода и <see cref="InvalidDataException"/> библиотека считает ответом «не мой».
    /// </summary>
    /// <param name="input">Вход с перемоткой.</param>
    /// <returns>Формат либо <c>null</c>, если тип не этого детектора.</returns>
    FileFormat? Detect(Stream input);
}
