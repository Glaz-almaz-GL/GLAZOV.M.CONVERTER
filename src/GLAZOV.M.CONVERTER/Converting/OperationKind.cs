namespace GLAZOV.M.CONVERTER.Converting;

/// <summary>
/// Вид действия над изображением. Нужен движкам расширений,
/// чтобы прочитать действие из параметров вызова и выполнить его.
/// </summary>
public enum OperationKind
{
    /// <summary>
    /// Повернуть вправо (по часовой стрелке) на 90 градусов.
    /// </summary>
    RotateRight,

    /// <summary>
    /// Изменить размер до заданного процента текущего; процент — <see cref="Operation.Percent"/>.
    /// </summary>
    ScalePercent,
}
