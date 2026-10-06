using System.IO.Compression;
using System.Text;
using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Detection;
using GLAZOV.M.CONVERTER.Errors;
using GLAZOV.M.CONVERTER.Settings;

namespace GLAZOV.M.CONVERTER.Tests.Detection;

/// <summary>
/// Определение SVG: UTF-16 с меткой порядка байтов и без неё, сжатый SVGZ, длинное вступление до корня
/// и окно определения как настройка.
/// </summary>
public sealed class SvgSignatureTests : IDisposable
{
    private const string Svg = """<svg xmlns="http://www.w3.org/2000/svg" width="10" height="10"><rect width="10" height="10"/></svg>""";
    private const string Declaration = """<?xml version="1.0" encoding="UTF-16"?>""";
    private const int Window = ConverterSettings.DefaultDetectionWindow;

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "convtests-" + Guid.NewGuid().ToString("N"));

    public SvgSignatureTests()
    {
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
    }

    private static byte[] Gzip(byte[] data)
    {
        using MemoryStream output = new();
        using (GZipStream gzip = new(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            gzip.Write(data);
        }

        return output.ToArray();
    }

    // Комментарий-вступление заданной длины в байтах UTF-8 (как у файлов с лицензией в шапке).
    private static string Prolog(int length)
    {
        return "<!-- " + new string('x', length) + " -->\n";
    }

    private static FileFormat Match(byte[] data, int window = Window)
    {
        return Sniff.Match(data.AsSpan(0, Math.Min(data.Length, window)), window);
    }

    private string Put(string name, byte[] data)
    {
        string path = Path.Combine(_folder, name);
        File.WriteAllBytes(path, data);
        return path;
    }

    [Theory]
    [InlineData("utf-16LE", true)]
    [InlineData("utf-16BE", true)]
    [InlineData("utf-16LE", false)]
    [InlineData("utf-16BE", false)]
    public void Utf16_WithAndWithoutByteOrderMark_IsSvg(string encodingName, bool withBom)
    {
        Encoding encoding = Encoding.GetEncoding(encodingName);
        byte[] text = encoding.GetBytes(Declaration + "\n" + Svg);
        byte[] data = withBom ? [.. encoding.GetPreamble(), .. text] : text;

        Assert.Same(FileFormat.Svg, Match(data));
    }

    [Fact]
    public void Utf16_WithoutDeclaration_StartingWithRoot_IsSvg()
    {
        Assert.Same(FileFormat.Svg, Match(Encoding.Unicode.GetBytes(Svg)));
    }

    [Fact]
    public void Utf16_XmlThatIsNotSvg_IsUnknown()
    {
        byte[] data = [.. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes(Declaration + "<html><svg/></html>")];

        Assert.Same(FileFormat.Unknown, Match(data));
    }

    [Fact]
    public void Utf16_CutInTheMiddleOfACharacter_DoesNotBreak()
    {
        byte[] data = [.. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes(Prolog(100) + Svg)];

        Assert.Same(FileFormat.Svg, Sniff.Match(data.AsSpan(0, data.Length - 1), Window));
    }

    [Fact]
    public void Svgz_IsSvg_InUtf8AndUtf16()
    {
        Assert.Same(FileFormat.Svg, Match(Gzip(Encoding.UTF8.GetBytes(Svg))));
        Assert.Same(FileFormat.Svg, Match(Gzip([.. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes(Svg)])));
    }

    [Fact]
    public void Gzip_OfSomethingElse_IsUnknown()
    {
        Assert.Same(FileFormat.Unknown, Match(Gzip(Encoding.UTF8.GetBytes("<html><body>не SVG</body></html>"))));
        Assert.Same(FileFormat.Unknown, Match(Gzip(new byte[5000])));
    }

    [Fact]
    public void Svgz_LargerThanTheWindow_IsRecognisedByItsBeginning()
    {
        StringBuilder big = new(Svg[..^6]);
        Random random = new(7);
        for (int i = 0; i < 20_000; i++)
        {
            big.Append($"<circle cx=\"{random.Next(1000)}\" cy=\"{random.Next(1000)}\" r=\"{random.Next(50)}\"/>");
        }

        byte[] compressed = Gzip(Encoding.UTF8.GetBytes(big.Append("</svg>").ToString()));

        Assert.True(compressed.Length > DetectionSizes.MinimumWindow);
        Assert.Same(FileFormat.Svg, Match(compressed, DetectionSizes.MinimumWindow));
    }

    [Fact]
    public void DamagedGzip_IsUnknown_WithoutException()
    {
        byte[] data = Gzip(Encoding.UTF8.GetBytes(Svg));
        for (int i = 10; i < data.Length; i++)
        {
            data[i] = 0xFF;
        }

        Assert.Same(FileFormat.Unknown, Match(data));
    }

    [Fact]
    public void LongProlog_FitsTheWindow_IsSvg_ButNotInTheSmallestWindow()
    {
        byte[] data = Encoding.UTF8.GetBytes("<?xml version=\"1.0\"?>\n" + Prolog(10_000) + Svg);

        Assert.Same(FileFormat.Svg, Match(data));
        Assert.Same(FileFormat.Unknown, Match(data, DetectionSizes.MinimumWindow));
    }

    [Fact]
    public void PrologLongerThanTheWindow_IsUnknown()
    {
        byte[] data = Encoding.UTF8.GetBytes(Prolog(Window + 10) + Svg);

        Assert.Same(FileFormat.Unknown, Match(data));
    }

    [Fact]
    public async Task PublicDetector_UsesTheDefaultWindow()
    {
        string path = Put("длинный.svg", Encoding.UTF8.GetBytes(Prolog(10_000) + Svg));

        Assert.Same(FileFormat.Svg, await FileTypeDetector.DetectAsync(Source.File(path)));
    }

    [Fact]
    public async Task DetectionWindow_IsASetting_ForTheConverter()
    {
        string path = Put("длинный.svg", Encoding.UTF8.GetBytes(Prolog(10_000) + Svg));
        Converter small = new(new ConverterSettings { DetectionWindow = DetectionSizes.MinimumWindow });
        Converter standard = new();

        // Окно мало — SVG не распознан, остаётся простой текст; окно по умолчанию — SVG распознан (движка SVG здесь нет, поэтому отказ уже по паре).
        ConversionException notRecognised = await Assert.ThrowsAsync<ConversionException>(async () =>
            await small.ConvertAsync(Source.File(path), Target.Memory(FileFormat.Png)));
        ConversionException recognised = await Assert.ThrowsAsync<ConversionException>(async () =>
            await standard.ConvertAsync(Source.File(path), Target.Memory(FileFormat.Png)));

        Assert.Equal(ConversionFailureReason.UnsupportedConversion, notRecognised.Reason);
        Assert.DoesNotContain("SVG", notRecognised.Message);
        Assert.Equal(ConversionFailureReason.UnsupportedConversion, recognised.Reason);
        Assert.Contains("SVG", recognised.Message);
    }

    [Fact]
    public async Task LongProlog_FromAStreamWithoutRewind_KeepsItsBeginning()
    {
        string path = Put("длинный.svg", Encoding.UTF8.GetBytes(Prolog(20_000) + Svg));
        await using Live.NonSeekableFile stream = new(path);

        ConversionException ex = await Assert.ThrowsAsync<ConversionException>(async () =>
            await new Converter().ConvertAsync(Source.Stream(stream), Target.Memory(FileFormat.Png)));

        Assert.Equal(ConversionFailureReason.UnsupportedConversion, ex.Reason);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4095)]
    public void DetectionWindow_BelowTheSmallest_IsRejected(int window)
    {
        ArgumentException ex = Assert.Throws<ArgumentException>(() => new Converter(new ConverterSettings { DetectionWindow = window }));

        Assert.Contains("DetectionWindow", ex.Message);
    }
}
