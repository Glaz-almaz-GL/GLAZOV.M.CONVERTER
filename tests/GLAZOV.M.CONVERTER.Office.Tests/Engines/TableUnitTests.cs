using System.Text;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Errors;
using GLAZOV.M.CONVERTER.Formats;
using GLAZOV.M.CONVERTER.Office.Engines;

namespace GLAZOV.M.CONVERTER.Office.Tests.Engines;

/// <summary>
/// The parts of the table engine that need no program: the arguments, the reading of delimited text (encoding, separator, size),
/// the limits of the sheet formats, the settings and the declared pairs.
/// </summary>
public sealed class TableUnitTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "convtable-" + Guid.NewGuid().ToString("N"));

    public TableUnitTests()
    {
        Directory.CreateDirectory(_folder);
        Encodings.Find("windows-1251", "test");
    }

    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
    }

    private string Write(string name, byte[] bytes)
    {
        string path = Path.Combine(_folder, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    [Fact]
    public void Engine_Declares32Pairs_SixOfThemWriteSeveralFiles()
    {
        TableEngine engine = new(new OfficeSettings());

        Assert.Equal(32, engine.Pairs.Count);
        Assert.Equal(32, engine.Pairs.Distinct().Count());
        Assert.Equal(25, engine.Pairs.Count(p => !p.From.Equals(FileFormat.Text) && !p.To.Equals(FileFormat.Pdf)));
        Assert.Equal(5, engine.Pairs.Count(p => p.From.Equals(FileFormat.Text)));
        Assert.Equal(2, engine.Pairs.Count(p => p.To.Equals(FileFormat.Pdf)));
        ConversionPairView[] several = [.. engine.Pairs.Where(p => p.WritesSeveralFiles).Select(p => new ConversionPairView(p.From.Extension, p.To.Extension))];
        Assert.Equal(6, several.Length);
        Assert.All(several, p => Assert.Contains(p.From, new[] { "xlsx", "xls", "ods" }));
        Assert.All(several, p => Assert.Contains(p.To, new[] { "csv", "tsv" }));
    }

    [Fact]
    public void Extension_PlugsBothEngines_AndDeclaresCsvAndTsv()
    {
        OfficeExtension extension = new();

        Assert.Equal(2, extension.Engines.Count);
        Assert.Contains(FileFormat.Csv, extension.Formats);
        Assert.Contains(FileFormat.Tsv, extension.Formats);
    }

    [Fact]
    public void Arguments_ConvertToCsv_WriteEverySheet_AsValues_WithTheMarkWhenAsked()
    {
        string to = TableArguments.ConvertTo(FileFormat.Csv, ';', '"', valuesAsShown: true, byteOrderMark: true);

        Assert.Equal("csv:Text - txt - csv (StarCalc):59,34,76,1,,0,false,true,false,false,false,-1,false,true", to);
        Assert.EndsWith(",false,false", TableArguments.ConvertTo(FileFormat.Tsv, '\t', '"', true, false), StringComparison.Ordinal);
        Assert.StartsWith("csv:Text - txt - csv (StarCalc):9,34,", TableArguments.ConvertTo(FileFormat.Tsv, '\t', '"', true, false), StringComparison.Ordinal);
        Assert.Contains(",false,false,false,false,false,-1,", TableArguments.ConvertTo(FileFormat.Csv, ',', '"', valuesAsShown: false, byteOrderMark: false), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("xlsx")]
    [InlineData("xls")]
    [InlineData("ods")]
    [InlineData("pdf")]
    public void Arguments_ConvertToOthers_IsTheExtension(string extension)
    {
        FileFormat format = FileFormat.Create(extension.ToUpperInvariant(), "application/x-test-" + extension, extension);

        Assert.Equal(extension, TableArguments.ConvertTo(format, ',', '"', true, true));
    }

    [Fact]
    public void Arguments_Import_IsUtf8_DoesNotEvaluateFormulas_AndCarriesTheSeparator()
    {
        Assert.Equal("Text - txt - csv (StarCalc):59,39,76,1,,0,false,true,false,false,false,,false", TableArguments.Import(';', '\''));
    }

    [Fact]
    public void Arguments_Run_IsHeadless_OwnProfile_FilterBeforeConvert()
    {
        IReadOnlyList<string> args = TableArguments.Convert(Path.Combine(_folder, "in.csv"), Path.Combine(_folder, "out"), Path.Combine(_folder, "p"), "FILTER", "xlsx");

        Assert.Contains("--headless", args);
        Assert.Contains("--infilter=FILTER", args);
        Assert.True(args.ToList().IndexOf("--infilter=FILTER") < args.ToList().IndexOf("--convert-to"));
        Assert.Equal("xlsx", args[args.ToList().IndexOf("--convert-to") + 1]);
        Assert.Equal(Path.Combine(_folder, "in.csv"), args[^1]);
        Assert.DoesNotContain(TableArguments.Convert("a.xlsx", "o", "p", null, "csv"), a => a.StartsWith("--infilter", StringComparison.Ordinal));
        Assert.Throws<ArgumentException>(() => TableArguments.Convert(" ", "o", "p", null, "csv"));
    }

    [Theory]
    [InlineData("a,b,c", "Csv", ',')]
    [InlineData("a;b;c", "Csv", ';')]
    [InlineData("a;\"b,c\";d", "Csv", ';')]
    [InlineData("a,b;c", "Csv", ',')]
    [InlineData("abc", "Csv", ',')]
    [InlineData("a;b", "Tsv", '\t')]
    [InlineData("a;b", "Text", ',')]
    public void Choose_TakesTheSettingWhenTheFirstRowHasIt_ElseASemicolonOrComma(string record, string kind, char expected)
    {
        FileFormat from = kind switch { "Csv" => FileFormat.Csv, "Tsv" => FileFormat.Tsv, _ => FileFormat.Text };

        Assert.Equal(expected, TableText.Choose(record, from, ',', '"'));
    }

    [Fact]
    public async Task Prepare_Utf8WithAndWithoutMark_Utf16_AreReadAsTheyAre()
    {
        string text = "Имя,Город\nИван,Москва\nПётр,Казань\n";
        foreach ((string name, byte[] bytes) in new[]
        {
            ("utf8.csv", Encoding.UTF8.GetBytes(text)),
            ("utf8bom.csv", [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(text)]),
            ("utf16.csv", [.. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes(text)]),
        })
        {
            string output = Path.Combine(_folder, "out-" + name);

            TableShape shape = await TableText.PrepareAsync(Write(name, bytes), output, FileFormat.Csv, new OfficeSettings(), CancellationToken.None);

            Assert.Equal(text, File.ReadAllText(output, new UTF8Encoding(false)));
            Assert.False(File.ReadAllBytes(output).AsSpan().StartsWith(Encoding.UTF8.GetPreamble()));
            Assert.Equal(new TableShape(',', 3, 2), shape);
        }
    }

    [Fact]
    public async Task Prepare_Windows1251_IsReadByTheSettingEncoding()
    {
        string text = "Имя;Город\nИван;Москва\nПётр;Казань\n";
        string output = Path.Combine(_folder, "out.csv");

        TableShape shape = await TableText.PrepareAsync(Write("cp1251.csv", Encoding.GetEncoding("windows-1251").GetBytes(text)), output, FileFormat.Csv, new OfficeSettings(), CancellationToken.None);

        Assert.Equal(text, File.ReadAllText(output, new UTF8Encoding(false)));
        Assert.Equal(';', shape.Delimiter);
    }

    [Fact]
    public async Task Prepare_BytesNeitherUtf8NorTheSettingEncoding_AreInvalidParameters()
    {
        byte[] bytes = [.. "a,b\n"u8.ToArray(), 0xE9, 0x98, .. ",x\n"u8.ToArray()];

        ConversionException error = await Assert.ThrowsAsync<ConversionException>(
            () => TableText.PrepareAsync(Write("bad.csv", bytes), Path.Combine(_folder, "o.csv"), FileFormat.Csv, new OfficeSettings { CsvInputEncoding = "us-ascii" }, CancellationToken.None));

        Assert.Equal(ConversionFailureReason.InvalidParameters, error.Reason);
        Assert.Contains("CsvInputEncoding", error.Message);
    }

    [Fact]
    public async Task Measure_CountsRowsAndTheWidestRow_QuotesHideSeparatorsAndBreaks()
    {
        string path = Write("m.csv", Encoding.UTF8.GetBytes("a,b,c\r\n\"x,y\nz\",2\r\n\r\n1,2,3,4"));

        TableShape shape = await TableText.MeasureAsync(path, ',', '"', CancellationToken.None);

        Assert.Equal(4, shape.Rows);
        Assert.Equal(4, shape.Columns);
    }

    [Theory]
    [InlineData("a,b\n", 1, 2)]
    [InlineData("a,b", 1, 2)]
    [InlineData("a,b\n\n", 2, 2)]
    [InlineData("", 0, 0)]
    [InlineData("\r\n\r\n", 2, 1)]
    public async Task Measure_EdgeCases(string text, long rows, int columns)
    {
        TableShape shape = await TableText.MeasureAsync(Write("e.csv", Encoding.UTF8.GetBytes(text)), ',', '"', CancellationToken.None);

        Assert.Equal((rows, columns), (shape.Rows, shape.Columns));
    }

    [Theory]
    [InlineData("xls", 65_537, 1, true)]
    [InlineData("xls", 65_536, 256, false)]
    [InlineData("xls", 10, 257, true)]
    [InlineData("xlsx", 1_048_577, 1, true)]
    [InlineData("xlsx", 1_048_576, 16_384, false)]
    [InlineData("xlsx", 10, 16_385, true)]
    [InlineData("ods", 1_048_577, 1, true)]
    [InlineData("ods", 10, 16_385, true)]
    [InlineData("csv", long.MaxValue, int.MaxValue, false)]
    public void Limits_RefuseWhatASheetCannotHold_NeverCut(string extension, long rows, int columns, bool refused)
    {
        FileFormat target = FileFormat.Create(extension.ToUpperInvariant(), "application/x-test-" + extension, extension);
        Action check = () => TableLimits.Check(new TableShape(',', rows, columns), target);

        if (refused)
        {
            ConversionException error = Assert.Throws<ConversionException>(check);
            Assert.Equal(ConversionFailureReason.InvalidParameters, error.Reason);
            Assert.Contains(extension.ToUpperInvariant(), error.Message);
        }
        else
        {
            check();
        }
    }

    [Fact]
    public void Settings_DefaultsAreValid_AndTheExtensionTakesACopy()
    {
        OfficeSettings settings = new() { CsvDelimiter = ';', SheetNumber = 2 };

        OfficeExtension extension = new(settings);
        settings.CsvDelimiter = '|';

        Assert.NotNull(extension);
        Assert.Equal(',', new OfficeSettings().CsvDelimiter);
        Assert.Equal("utf-8", new OfficeSettings().CsvOutputEncoding);
        Assert.True(new OfficeSettings().CsvOutputByteOrderMark);
        Assert.Equal(1, new OfficeSettings().SheetNumber);
    }

    [Fact]
    public void Arguments_Import_CanReadLeadingColumnsAsText()
    {
        Assert.Equal("Text - txt - csv (StarCalc):44,34,76,1,1/2/2/2/3/2,0,false,true,false,false,false,,false", TableArguments.Import(',', '"', textColumns: 3));
    }

    [Theory]
    [InlineData("xlsx")]
    [InlineData("ods")]
    public void BookSize_ReadsTheLargestSheet_WithoutLibreOffice(string extension)
    {
        FileFormat format = extension == "xlsx" ? FileFormat.Xlsx : FileFormat.Ods;

        TableShape? size = BookSize.Measure(Path.Combine(AppContext.BaseDirectory, "Samples", "sample-book." + extension), format);

        Assert.Equal((2L, 2), (size!.Value.Rows, size.Value.Columns));
    }

    [Fact]
    public void BookSize_UnreadableOrOtherFormats_GiveNull()
    {
        string path = Write("junk.xlsx", [1, 2, 3]);

        Assert.Null(BookSize.Measure(path, FileFormat.Xlsx));
        Assert.Null(BookSize.Measure(path, FileFormat.Xls));
    }

    [Fact]
    public void Settings_KeepValuesAsText_IsOnByDefault()
    {
        Assert.True(new OfficeSettings().CsvKeepValuesAsText);
    }

    [Fact]
    public void Settings_WrongValues_AreRefusedWithTheNameOfTheProperty()
    {
        Assert.Equal("CsvDelimiter", Assert.Throws<ArgumentException>(() => new OfficeExtension(new OfficeSettings { CsvDelimiter = '\n' })).ParamName);
        Assert.Equal("CsvDelimiter", Assert.Throws<ArgumentException>(() => new OfficeExtension(new OfficeSettings { CsvDelimiter = '"' })).ParamName);
        Assert.Equal("CsvQuote", Assert.Throws<ArgumentException>(() => new OfficeExtension(new OfficeSettings { CsvQuote = '\r' })).ParamName);
        Assert.Equal("SheetNumber", Assert.Throws<ArgumentOutOfRangeException>(() => new OfficeExtension(new OfficeSettings { SheetNumber = 0 })).ParamName);
        Assert.Equal("CsvOutputEncoding", Assert.Throws<ArgumentException>(() => new OfficeExtension(new OfficeSettings { CsvOutputEncoding = "no-such-encoding" })).ParamName);
        Assert.Equal("CsvInputEncoding", Assert.Throws<ArgumentException>(() => new OfficeExtension(new OfficeSettings { CsvInputEncoding = " " })).ParamName);
    }

    private sealed record ConversionPairView(string From, string To);
}
