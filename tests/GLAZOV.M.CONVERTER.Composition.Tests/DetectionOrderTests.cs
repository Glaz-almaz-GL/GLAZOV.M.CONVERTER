using System.IO.Compression;
using System.Text;
using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Formats;
using GLAZOV.M.CONVERTER.Settings;

namespace GLAZOV.M.CONVERTER.Composition.Tests;

/// <summary>
/// The six packages share one row of detectors: every sample is named the same way whatever the order the packages are plugged in.
/// </summary>
public sealed class DetectionOrderTests : IDisposable
{
    private static readonly string[] Orders = [.. AllPackages.Orders()];

    private static readonly Dictionary<string, string> Real = new()
    {
        ["sample-audioonly.mkv"] = "MKV", ["sample-audioonly.mp4"] = "MP4", ["sample-id3.mp3"] = "MP3", ["sample-macro.odt"] = "ODT",
        ["sample-notsvg.xml"] = "Text", ["sample-opus.ogg"] = "OGG", ["sample-plain.svg"] = "SVG", ["sample-prolog.svg"] = "SVG",
        ["sample-raw.mp3"] = "MP3", ["sample-vorbis.ogg"] = "OGG", ["sample.avi"] = "AVI", ["sample.bmp"] = "BMP", ["sample.dds"] = "DDS",
        ["sample.doc"] = "DOC", ["sample.docx"] = "DOCX", ["sample.flac"] = "FLAC", ["sample.gif"] = "GIF", ["sample.html"] = "HTML",
        ["sample.jpg"] = "JPEG", ["sample.m4a"] = "M4A", ["sample.mkv"] = "MKV", ["sample.mov"] = "MOV", ["sample.mp4"] = "MP4",
        ["sample.odp"] = "ODP", ["sample.ods"] = "ODS", ["sample.odt"] = "ODT", ["sample.pdf"] = "PDF", ["sample.png"] = "PNG",
        ["sample.ppt"] = "PPT", ["sample.pptx"] = "PPTX", ["sample.psd"] = "PSD", ["sample.rtf"] = "RTF", ["sample.tga"] = "Unknown",
        ["sample.tiff"] = "TIFF", ["sample.wav"] = "WAV", ["sample.webm"] = "WebM", ["sample.webp"] = "WebP", ["sample.xls"] = "XLS",
        ["sample.xlsx"] = "XLSX", ["sample.zip"] = "ZIP", ["sample-empty.zip"] = "ZIP", ["sample.jar"] = "ZIP", ["sample.epub"] = "ZIP",
        ["sample-big.zip"] = "ZIP", ["sample.tar"] = "TAR", ["sample-v7.tar"] = "TAR", ["sample-gnu.tar"] = "TAR", ["sample.tar.gz"] = "TAR.GZ",
        ["sample.gz"] = "GZip",
    };

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "convcomp-" + Guid.NewGuid().ToString("N"));

    public DetectionOrderTests()
    {
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
    }

    public static TheoryData<string> RealSamples => [.. Real.Keys];

    private static string Sample(string name)
    {
        return Path.Combine(AppContext.BaseDirectory, "Samples", name);
    }

    private string Put(string name, byte[] bytes)
    {
        string path = Path.Combine(_folder, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static async Task<string> DetectAsync(string order, string path)
    {
        return (await AllPackages.Converter(order).DetectAsync(Source.File(path))).Name;
    }

    [Fact]
    public void ThereAreSevenHundredAndTwentyOrders()
    {
        Assert.Equal(720, Orders.Length);
    }

    [Theory]
    [MemberData(nameof(RealSamples))]
    public async Task RealSample_IsNamedTheSame_InEveryOrder(string file)
    {
        foreach (string order in Orders)
        {
            Assert.True(Real[file] == await DetectAsync(order, Sample(file)), $"{file}: order {order} gave another name than {Real[file]}");
        }
    }

    [Fact]
    public async Task SyntheticInputs_AreNamedTheSame_InEveryOrder()
    {
        string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"4\" height=\"4\"><rect width=\"4\" height=\"4\"/></svg>";
        using MemoryStream packed = new();
        await using (GZipStream gzip = new(packed, CompressionLevel.Optimal, leaveOpen: true))
        {
            await gzip.WriteAsync(Encoding.UTF8.GetBytes(svg));
        }

        byte[] docx = File.ReadAllBytes(Sample("sample.docx"));
        byte[] random = new byte[2048];
        new Random(7).NextBytes(random);
        Dictionary<string, (byte[] Bytes, string Expected)> inputs = new()
        {
            ["пустой"] = ([], "Unknown"),
            ["svgz"] = (packed.ToArray(), "SVG"),
            ["utf16"] = ([.. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes(svg)], "SVG"),
            ["текст"] = (Encoding.UTF8.GetBytes("Просто строка текста без разметки."), "Text"),
            ["хвост обрезан"] = (docx[..600], "DOCX"),
            ["обрезан до четырёх байтов"] = (docx[..4], "Unknown"),
            ["мусор"] = (random, "Unknown"),
        };

        foreach ((string name, (byte[] bytes, string expected)) in inputs)
        {
            string path = Put(name + ".bin", bytes);
            foreach (string order in Orders)
            {
                string actual = await DetectAsync(order, path); Assert.True(expected == actual, $"{name}: order {order} gave {actual}, not {expected}");
            }
        }
    }

    [Fact]
    public async Task TheFormatsPackageAlone_NamesEverySampleLikeTheFullSet()
    {
        ConverterSettings settings = new();
        settings.Extensions.Add(new FormatsExtension());
        Converter alone = new(settings);

        foreach ((string file, string expected) in Real)
        {
            Assert.Equal(expected, (await alone.DetectAsync(Source.File(Sample(file)))).Name);
        }
    }
}
