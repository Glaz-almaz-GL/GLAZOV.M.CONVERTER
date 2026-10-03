using GLAZOV.M.CONVERTER.Capabilities;
using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;

namespace GLAZOV.M.CONVERTER.Engines;

/// <summary>
/// Что просят у движка: вход и выход потоками, подтверждённый формат входа и формат результата, параметры вызова целиком,
/// найденная программа и папка временных файлов.
/// </summary>
/// <param name="Input">Читаемый поток со всем входом.</param>
/// <param name="Output">Поток, в который движок пишет результат.</param>
/// <param name="From">Подтверждённый по начальным байтам формат входа.</param>
/// <param name="To">Формат результата.</param>
/// <param name="Options">Параметры вызова (звук, изображение, действия); пусто — всё из источника и умолчаний движка.</param>
/// <param name="Tool">Найденная внешняя программа движка.</param>
/// <param name="TempFolder">Существующая папка для временных файлов движка.</param>
internal sealed record EngineRequest(
    Stream Input,
    Stream Output,
    FileFormat From,
    FileFormat To,
    ConversionOptions? Options,
    ToolStatus Tool,
    string TempFolder);
