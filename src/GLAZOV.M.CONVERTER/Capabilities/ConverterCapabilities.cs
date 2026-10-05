using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Engines;
using GLAZOV.M.CONVERTER.Tooling;

namespace GLAZOV.M.CONVERTER.Capabilities;

/// <summary>
/// Что библиотека поддерживает сейчас и какие внешние программы найдены.
/// </summary>
public sealed class ConverterCapabilities
{
    private readonly EngineRegistry _registry;
    private readonly ToolLocator _locator;
    private readonly IReadOnlyList<FileFormat> _formats;

    internal ConverterCapabilities(EngineRegistry registry, ToolLocator locator, IReadOnlyList<FileFormat>? formats = null)
    {
        _registry = registry;
        _locator = locator;
        _formats = formats ?? [];
    }

    /// <summary>
    /// Все форматы, объявленные подключёнными расширениями, без повторов, по порядку подключения.
    /// </summary>
    public IReadOnlyList<FileFormat> Formats => _formats;

    /// <summary>
    /// Все поддерживаемые конвертации с пометкой «готово» или «нужна программа»; порядок стабильный.
    /// </summary>
    public IReadOnlyList<Conversion> All
    {
        get
        {
            return
            [
                .. _registry.Pairs().Select(pair => new Conversion(
                    pair.From,
                    pair.To,
                    pair.ToolName is null || _locator.Locate(pair.ToolName).Found ? ConversionAvailability.Ready : ConversionAvailability.ToolMissing)),
            ];
        }
    }

    /// <summary>
    /// Поддерживается ли конвертация из типа в формат — независимо от того, найдена ли нужная программа.
    /// </summary>
    /// <param name="from">Тип входа.</param>
    /// <param name="to">Формат результата.</param>
    public bool CanConvert(FileFormat from, FileFormat to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        return _registry.Find(from, to) is not null;
    }

    /// <summary>
    /// Состояние внешней программы по имени. Неизвестное имя — <see cref="ArgumentException"/> с перечнем известных.
    /// </summary>
    /// <param name="name">Имя программы, например ffmpeg.</param>
    public ToolStatus Tool(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        string[] known = [.. _registry.Engines.Select(engine => engine.ToolName).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase)];
        if (!known.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Программа «{name}» библиотеке неизвестна. Известные: {string.Join(", ", known)}.", nameof(name));
        }

        return _locator.Locate(name);
    }
}
