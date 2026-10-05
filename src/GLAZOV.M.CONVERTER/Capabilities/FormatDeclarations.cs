using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Extensions;

namespace GLAZOV.M.CONVERTER.Capabilities;

/// <summary>
/// Форматы, объявленные подключёнными расширениями: равные значения в нескольких расширениях — норма, два разных с одним MIME-типом — ошибка.
/// </summary>
internal static class FormatDeclarations
{
    /// <summary>
    /// Собирает различные форматы по порядку подключения; расхождение описаний — <see cref="ArgumentException"/> с именами обоих расширений.
    /// </summary>
    /// <param name="extensions">Подключённые расширения по порядку.</param>
    internal static IReadOnlyList<FileFormat> Collect(IReadOnlyList<IConverterExtension> extensions)
    {
        List<FileFormat> formats = [];
        Dictionary<string, (string Extension, FileFormat Format)> byMime = new(StringComparer.OrdinalIgnoreCase);
        foreach (IConverterExtension extension in extensions)
        {
            foreach (FileFormat format in extension.Formats)
            {
                if (!byMime.TryGetValue(format.MimeType, out (string Extension, FileFormat Format) first))
                {
                    byMime[format.MimeType] = (extension.Name, format);
                    formats.Add(format);
                }
                else if (!first.Format.Equals(format))
                {
                    throw new ArgumentException(
                        $"Расширения «{first.Extension}» и «{extension.Name}» описывают формат с MIME-типом «{format.MimeType}» по-разному: "
                        + $"«{first.Extension}» — {Describe(first.Format)}, «{extension.Name}» — {Describe(format)}. Описания должны совпадать.",
                        nameof(extensions));
                }
            }
        }

        return formats;
    }

    private static string Describe(FileFormat format)
    {
        return $"имя {format.Name}, MIME-тип {format.MimeType}, расширение {format.Extension}";
    }
}
