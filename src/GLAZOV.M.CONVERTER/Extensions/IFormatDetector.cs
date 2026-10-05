using GLAZOV.M.CONVERTER.Data;

namespace GLAZOV.M.CONVERTER.Extensions;

/// <summary>
/// Определение формата по начальным байтам (расширение имени не учитывается).
/// Порядок опроса: метки всех расширений в порядке подключения, затем <see cref="IStreamFormatDetector"/>, затем определители
/// по содержимому (<see cref="ByContent"/>); побеждает первый ответивший.
/// </summary>
public interface IFormatDetector
{
    /// <summary>
    /// Сколько начальных байтов нужно детектору; библиотека читает наибольшее из запрошенных значений и окна определения.
    /// </summary>
    int HeadLength { get; }

    /// <summary>
    /// <c>true</c> — формат угадывается по содержимому, а не по метке в начале: такой определитель спрашивается после всех остальных.
    /// По умолчанию <c>false</c>.
    /// </summary>
    bool ByContent => false;

    /// <summary>
    /// Определяет формат по началу данных.
    /// </summary>
    /// <param name="head">Начальные байты входа: окно определения, но не короче <see cref="HeadLength"/>; короче, если вход короче.</param>
    /// <returns>Формат либо <c>null</c>, если тип не этого детектора.</returns>
    FileFormat? Detect(ReadOnlySpan<byte> head);
}
