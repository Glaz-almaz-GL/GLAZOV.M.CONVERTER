using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Detection;
using GLAZOV.M.CONVERTER.Internal;

namespace GLAZOV.M.CONVERTER.Capabilities;

/// <summary>
/// Что библиотека поддерживает сейчас и какие внешние программы найдены.
/// </summary>
public sealed class ConverterCapabilities
{
    internal ConverterCapabilities()
    {
    }

    /// <summary>
    /// Все поддерживаемые конвертации с пометкой «готово» или «нужна программа».
    /// </summary>
    public IReadOnlyList<Conversion> All => throw Stub.NotYet("ConverterCapabilities.All");

    /// <summary>
    /// Поддерживается ли конвертация из типа в формат.
    /// </summary>
    /// <param name="from">Тип входа.</param>
    /// <param name="to">Формат результата.</param>
    public bool CanConvert(FileType from, Format to)
    {
        ArgumentNullException.ThrowIfNull(from);
        throw Stub.NotYet("ConverterCapabilities.CanConvert");
    }

    /// <summary>
    /// Состояние внешней программы по имени.
    /// </summary>
    /// <param name="name">Имя программы, например ffmpeg.</param>
    public ToolStatus Tool(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        throw Stub.NotYet("ConverterCapabilities.Tool");
    }
}
