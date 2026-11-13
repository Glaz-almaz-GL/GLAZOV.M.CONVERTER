using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Engines;
using GLAZOV.M.CONVERTER.Errors;
using GLAZOV.M.CONVERTER.Extensions;
using GLAZOV.M.CONVERTER.Settings;
using GLAZOV.M.CONVERTER.Tests.Extension;

namespace GLAZOV.M.CONVERTER.Tests.Converting;

/// <summary>
/// The names an engine may give to a result file: one rule for all three systems, refused never cleaned.
/// </summary>
public sealed class ResultFileNameTests
{
    private static readonly string Folder = Path.Combine(Path.GetTempPath(), "convtests-names");

    private static ConversionRequest Request(string? outputFolder)
    {
        return new ConversionRequest(Stream.Null, Stream.Null, TestFormats.Alpha, TestFormats.Beta, new ConversionOptions(), null, Path.GetTempPath()) { OutputFolder = outputFolder };
    }

    [Theory]
    [InlineData("..")]
    [InlineData("..\\evil.csv")]
    [InlineData("../evil.csv")]
    [InlineData("a..b.csv")]
    [InlineData("a/b.csv")]
    [InlineData("a\\b.csv")]
    [InlineData("C:x.csv")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("name.")]
    [InlineData("name ")]
    [InlineData("name. ")]
    [InlineData("CON")]
    [InlineData("con.csv")]
    [InlineData("nul.txt")]
    [InlineData("NUL .txt")]
    [InlineData("COM1")]
    [InlineData("lpt9.csv")]
    [InlineData("a<b")]
    [InlineData("a>b")]
    [InlineData("a\"b")]
    [InlineData("a|b")]
    [InlineData("a?b")]
    [InlineData("a*b")]
    [InlineData("a\u0001b")]
    [InlineData("a\tb")]
    [InlineData("a\u007Fb")]
    public void BadNames_AreRefused(string name)
    {
        Assert.Throws<ArgumentException>(() => Request(Folder).GetOutputFilePath(name));
    }

    [Fact]
    public void Length_255IsFine_256IsRefused()
    {
        Assert.NotNull(Request(Folder).GetOutputFilePath(new string('a', 255)));
        Assert.Throws<ArgumentException>(() => Request(Folder).GetOutputFilePath(new string('a', 256)));
    }

    [Theory]
    [InlineData("book-Итог.csv")]
    [InlineData("отчёт за год.csv")]
    [InlineData("a.b.c.csv")]
    [InlineData(".hidden")]
    [InlineData("console.csv")]
    [InlineData("COM10.csv")]
    [InlineData("x")]
    public void GoodNames_GiveAPathInsideTheFolder(string name)
    {
        string path = Request(Folder).GetOutputFilePath(name);

        Assert.Equal(Path.Combine(Folder, name), path);
        Assert.Equal(Folder, Path.GetDirectoryName(path));
    }

    [Fact]
    public void NoOutputFolder_IsAnInvalidOperation()
    {
        Assert.Throws<InvalidOperationException>(() => Request(null).GetOutputFilePath("a.csv"));
    }

    [Fact]
    public void TheMethodCreatesNothing()
    {
        Request(Folder).GetOutputFilePath("a.csv");

        Assert.False(Directory.Exists(Folder));
    }

    [Fact]
    public async Task AnEngineThatWritesABadName_FailsTheConversion_AndLeavesNothing()
    {
        string root = Path.Combine(Path.GetTempPath(), "convtests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string input = Path.Combine(root, "book.alpha");
            File.WriteAllBytes(input, TestFormats.Sample(TestFormats.Alpha));
            MultiFileEngine engine = new(TestFormats.Alpha, TestFormats.Beta) { SheetName = (stem, i) => i == 2 ? "..\\evil.beta" : $"{stem}-{i}.beta" };
            ConverterSettings settings = new() { TempFolder = root };
            settings.Extensions.Add(TestExtensions.Create());
            Converter converter = new(settings, new EngineRegistry([engine]), null);
            string output = Path.Combine(root, "out");

            ConversionException error = await Assert.ThrowsAsync<ConversionException>(
                () => converter.ConvertAsync(Source.File(input), Target.Folder(output, TestFormats.Beta)));

            Assert.Equal(ConversionFailureReason.EngineFailed, error.Reason);
            Assert.Empty(Directory.GetFileSystemEntries(output, "*", SearchOption.AllDirectories));
            Assert.False(File.Exists(Path.Combine(root, "evil.beta")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
