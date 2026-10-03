using GLAZOV.M.CONVERTER.Capabilities;
using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;

namespace GLAZOV.M.CONVERTER.Engines;

/// <summary>
/// Что просят у движка: вход и выход потоками, формат результата, параметры звука, найденная программа и папка временных файлов.
/// </summary>
/// <param name="Input">Читаемый поток со всем входом.</param>
/// <param name="Output">Поток, в который движок пишет результат.</param>
/// <param name="To">Формат результата.</param>
/// <param name="Audio">Параметры звука; пусто — всё из источника и умолчаний движка.</param>
/// <param name="Tool">Найденная внешняя программа движка.</param>
/// <param name="TempFolder">Существующая папка для временных файлов движка.</param>
internal sealed record EngineRequest(
    Stream Input,
    Stream Output,
    FileFormat To,
    AudioOptions? Audio,
    ToolStatus Tool,
    string TempFolder);
