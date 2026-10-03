namespace GLAZOV.M.CONVERTER.IO;

/// <summary>
/// Итог записи: путь к файлу, либо байты и читаемый поток из памяти, либо ничего (записано в чужой поток).
/// </summary>
/// <param name="OutputPath">Путь к файлу результата.</param>
/// <param name="Bytes">Данные результата в памяти.</param>
/// <param name="Stream">Читаемый поток с результатом в памяти.</param>
internal sealed record SinkResult(string? OutputPath, byte[]? Bytes, Stream? Stream);
