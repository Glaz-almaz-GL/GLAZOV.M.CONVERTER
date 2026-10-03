using GLAZOV.M.CONVERTER.Data;

namespace GLAZOV.M.CONVERTER.Extensions;

/// <summary>
/// Определение формата по начальным байтам (расширение имени не учитывается).
/// Сначала спрашиваются готовые признаки библиотеки, затем признаки расширений в порядке подключения; побеждает первый ответивший.
/// </summary>
public interface IFormatDetector
{
    /// <summary>
    /// Сколько начальных байтов нужно детектору; библиотека читает наибольшее из запрошенных значений.
    /// </summary>
    int HeadLength { get; }

    /// <summary>
    /// Определяет формат по началу данных.
    /// </summary>
    /// <param name="head">Начальные байты входа (их может быть меньше <see cref="HeadLength"/>, если вход короче).</param>
    /// <returns>Формат либо <c>null</c>, если тип не этого детектора.</returns>
    FileFormat? Detect(ReadOnlySpan<byte> head);
}
