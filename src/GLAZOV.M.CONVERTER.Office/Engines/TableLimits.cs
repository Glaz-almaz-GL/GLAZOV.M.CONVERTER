using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Errors;

namespace GLAZOV.M.CONVERTER.Office.Engines;

/// <summary>
/// The size a sheet of each table format can hold; a text bigger than that is refused, never cut.
/// </summary>
internal static class TableLimits
{
    /// <summary>
    /// Throws InvalidParameters when the text does not fit a sheet of <paramref name="target"/>.
    /// </summary>
    /// <param name="shape">Size of the text.</param>
    /// <param name="target">Result format.</param>
    internal static void Check(TableShape shape, FileFormat target)
    {
        (long rows, int columns, string name) = target.Extension switch
        {
            "xls" => (65_536L, 256, "XLS"),
            "xlsx" => (1_048_576L, 16_384, "XLSX"),
            "ods" => (1_048_576L, 16_384, "ODS"),
            _ => (long.MaxValue, int.MaxValue, string.Empty),
        };

        if (shape.Rows > rows)
        {
            throw ConversionException.InvalidParameters($"В таблице {shape.Rows} строк, а лист {name} вмещает не больше {rows}: уменьшите таблицу или выберите другой формат.");
        }

        if (shape.Columns > columns)
        {
            throw ConversionException.InvalidParameters($"В таблице {shape.Columns} столбцов, а лист {name} вмещает не больше {columns}: уменьшите таблицу или выберите другой формат.");
        }
    }
}
