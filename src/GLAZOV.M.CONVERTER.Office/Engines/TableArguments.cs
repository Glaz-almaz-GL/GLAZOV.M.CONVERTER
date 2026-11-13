using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Formats;

namespace GLAZOV.M.CONVERTER.Office.Engines;

/// <summary>
/// Arguments of LibreOffice for converting a table: pure functions that start no program.
/// Every run has its own profile, as in <see cref="LibreOfficeArguments"/>.
/// </summary>
internal static class TableArguments
{
    private const string TextFilter = "Text - txt - csv (StarCalc)";

    /// <summary>
    /// The arguments of a headless run.
    /// </summary>
    /// <param name="inputPath">The file to convert.</param>
    /// <param name="outputFolder">Folder for the result.</param>
    /// <param name="profileFolder">Folder of the user profile of this run only.</param>
    /// <param name="importFilter">Value for <c>--infilter</c> when the input is delimited text; <c>null</c> for a book.</param>
    /// <param name="convertTo">Value for <c>--convert-to</c>.</param>
    internal static IReadOnlyList<string> Convert(string inputPath, string outputFolder, string profileFolder, string? importFilter, string convertTo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputFolder);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileFolder);
        ArgumentException.ThrowIfNullOrWhiteSpace(convertTo);

        List<string> arguments =
        [
            $"-env:UserInstallation={new Uri(Path.GetFullPath(profileFolder)).AbsoluteUri}",
            "--headless",
            "--norestore",
            "--nolockcheck",
            "--nologo",
        ];

        if (importFilter is not null)
        {
            arguments.Add($"--infilter={importFilter}");
        }

        arguments.AddRange(["--convert-to", convertTo, "--outdir", outputFolder, inputPath]);
        return arguments;
    }

    /// <summary>
    /// The import filter of delimited text: UTF-8, first row is data, formulas are not evaluated (a cell that starts with an equals sign stays text).
    /// </summary>
    /// <param name="delimiter">Field separator.</param>
    /// <param name="quote">Text delimiter.</param>
    /// <param name="textColumns">Number of leading columns read as text, so that numbers keep their form; 0 reads numbers as numbers.</param>
    internal static string Import(char delimiter, char quote, int textColumns = 0)
    {
        string columns = string.Join('/', Enumerable.Range(1, textColumns).Select(column => $"{column}/2"));
        return $"{TextFilter}:{(int)delimiter},{(int)quote},76,1,{columns},0,false,true,false,false,false,,false";
    }

    /// <summary>
    /// The value of <c>--convert-to</c> for the format; delimited text writes every sheet to its own file, formulas are written as values.
    /// </summary>
    /// <param name="target">Result format.</param>
    /// <param name="delimiter">Field separator of a CSV or TSV result.</param>
    /// <param name="quote">Text delimiter of a CSV or TSV result.</param>
    /// <param name="valuesAsShown">Numbers and dates as they look in the sheet.</param>
    /// <param name="byteOrderMark">Whether LibreOffice itself writes the UTF-8 mark.</param>
    internal static string ConvertTo(FileFormat target, char delimiter, char quote, bool valuesAsShown, bool byteOrderMark)
    {
        if (target.Equals(FileFormat.Csv) || target.Equals(FileFormat.Tsv))
        {
            return $"csv:{TextFilter}:{(int)delimiter},{(int)quote},76,1,,0,false,{Flag(valuesAsShown)},false,false,false,-1,false,{Flag(byteOrderMark)}";
        }

        return target.Extension;
    }

    private static string Flag(bool value)
    {
        return value ? "true" : "false";
    }
}
