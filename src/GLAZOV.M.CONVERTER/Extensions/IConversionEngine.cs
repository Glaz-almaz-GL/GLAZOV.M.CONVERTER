namespace GLAZOV.M.CONVERTER.Extensions;

/// <summary>
/// Движок конвертации: умеет переводить входы одних форматов в другие. Ничего не знает о том, откуда пришёл вход
/// и куда пойдёт результат, — только о потоках. Готовые движки библиотеки подключаются тем же путём.
/// Движок выполняется в процессе вызывающего: подключайте только тот, которому доверяете.
/// </summary>
public interface IConversionEngine
{
    /// <summary>
    /// Внешние программы, которые нужны движку; пусто — программа не нужна. Библиотека найдёт их общим поиском по описаниям
    /// и передаст в <see cref="ConversionRequest.Tools"/>; первая обязательная — в <see cref="ConversionRequest.Tool"/>.
    /// Не найденная обязательная программа — отказ до запуска работы с подсказкой установки.
    /// </summary>
    IReadOnlyList<ToolSpec> Tools => [];

    /// <summary>
    /// Умеет ли движок действия над изображением (поворот, масштаб) из параметров вызова.
    /// Движок, не умеющий их, получает отказ до запуска работы — молча игнорировать действия нельзя.
    /// </summary>
    bool SupportsOperations { get; }

    /// <summary>
    /// Все пары «из какого формата в какой», которые умеет движок.
    /// </summary>
    IReadOnlyList<ConversionPair> Pairs { get; }

    /// <summary>
    /// Выполняет конвертацию потоками. Непредвиденное исключение библиотека превращает в ошибку конвертации
    /// (причина «ошибка движка», этап «конвертация»); отмена — <see cref="OperationCanceledException"/>.
    /// </summary>
    /// <param name="request">Вход, выход и параметры.</param>
    /// <param name="cancellationToken">Отмена.</param>
    Task ConvertAsync(ConversionRequest request, CancellationToken cancellationToken);
}
