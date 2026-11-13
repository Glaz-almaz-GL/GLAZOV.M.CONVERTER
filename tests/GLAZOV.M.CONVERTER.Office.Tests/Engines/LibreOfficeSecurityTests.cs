using System.IO.Compression;
using System.Net.Sockets;
using System.Net;
using System.Text.RegularExpressions;
using System.Text;
using System.Xml.Linq;
using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Formats;
using GLAZOV.M.CONVERTER.Office.Engines;
using GLAZOV.M.CONVERTER.Settings;
using UglyToad.PdfPig;

namespace GLAZOV.M.CONVERTER.Office.Tests.Engines;

/// <summary>
/// A document is untrusted input: the profile of every LibreOffice run turns macros off and blocks links to the outside.
/// Live checks use a local server that counts connections (a connection means the document reached out) and a document with a macro.
/// </summary>
[Collection("LibreOffice")]
public sealed partial class LibreOfficeSecurityTests : IDisposable
{
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "convoffice-sec-" + Guid.NewGuid().ToString("N"));

    public LibreOfficeSecurityTests()
    {
        Directory.CreateDirectory(Path.Combine(_temp, "work"));
    }

    public void Dispose()
    {
        Directory.Delete(_temp, recursive: true);
    }

    [GeneratedRegex(@"ConvertToURL\(&quot;MARKER_PATH&quot;\)")]
    private static partial Regex MarkerCall();

    private Converter Converter(OfficeSettings? office = null)
    {
        return OfficeConverter.Create(new ConverterSettings { TempFolder = Path.Combine(_temp, "work") }, office);
    }

    private static string[] Pairs(string profile)
    {
        XDocument settings = XDocument.Parse(profile);
        return [.. settings.Descendants("item").Select(item => $"{item.Elements("prop").Single().Attribute(XName.Get("name", "http://openoffice.org/2001/registry"))!.Value}={item.Descendants("value").Single().Value}")];
    }

    /// <summary>
    /// A server that answers every request with a tiny picture and counts the connections.
    /// </summary>
    private sealed class CountingServer : IDisposable
    {
        private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _stop = new();
        private int _connections;

        internal CountingServer()
        {
            _listener.Start();
            _ = Task.Run(AcceptAsync);
        }

        internal int Connections => Volatile.Read(ref _connections);

        internal string Url(string name) => $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/{name}";

        public void Dispose()
        {
            _stop.Cancel();
            _listener.Stop();
            _stop.Dispose();
        }

        private async Task AcceptAsync()
        {
            try
            {
                while (!_stop.IsCancellationRequested)
                {
                    using TcpClient client = await _listener.AcceptTcpClientAsync(_stop.Token);
                    Interlocked.Increment(ref _connections);
                    await using NetworkStream stream = client.GetStream();
                    byte[] header = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: image/png\r\nContent-Length: {Png.Length}\r\nConnection: close\r\n\r\n");
                    await stream.WriteAsync(header);
                    await stream.WriteAsync(Png);
                }
            }
            catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException or SocketException or IOException)
            {
                // The server was stopped.
            }
        }
    }

    private static string DocxWithLinkedPicture(string pictureUrl)
    {
        const string xml = """<?xml version="1.0" encoding="UTF-8"?>""";
        string path = Path.Combine(Path.GetTempPath(), "linked-" + Guid.NewGuid().ToString("N") + ".docx");
        using ZipArchive zip = ZipFile.Open(path, ZipArchiveMode.Create);
        void Add(string name, string content)
        {
            using StreamWriter writer = new(zip.CreateEntry(name).Open(), new UTF8Encoding(false));
            writer.Write(content);
        }

        Add("[Content_Types].xml", xml + """<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/></Types>""");
        Add("_rels/.rels", xml + """<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/></Relationships>""");
        Add("word/_rels/document.xml.rels", xml + $"""<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId5" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/image" Target="{pictureUrl}" TargetMode="External"/></Relationships>""");
        Add("word/document.xml", xml + """<w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships" xmlns:wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:pic="http://schemas.openxmlformats.org/drawingml/2006/picture"><w:body><w:p><w:r><w:t>Связанная картинка</w:t></w:r></w:p><w:p><w:r><w:drawing><wp:inline distT="0" distB="0" distL="0" distR="0"><wp:extent cx="914400" cy="914400"/><wp:docPr id="1" name="p"/><a:graphic><a:graphicData uri="http://schemas.openxmlformats.org/drawingml/2006/picture"><pic:pic><pic:nvPicPr><pic:cNvPr id="0" name="p"/><pic:cNvPicPr/></pic:nvPicPr><pic:blipFill><a:blip r:link="rId5"/><a:stretch><a:fillRect/></a:stretch></pic:blipFill><pic:spPr><a:xfrm><a:off x="0" y="0"/><a:ext cx="914400" cy="914400"/></a:xfrm><a:prstGeom prst="rect"><a:avLst/></a:prstGeom></pic:spPr></pic:pic></a:graphicData></a:graphic></wp:inline></w:drawing></w:r></w:p></w:body></w:document>""");
        return path;
    }

    [Fact]
    public void Profile_TurnsMacrosOff_AndBlocksOutsideLinks()
    {
        string[] pairs = Pairs(LibreOfficeProfile.Settings);
        Assert.Contains("MacroSecurityLevel=3", pairs);
        Assert.Contains("DisableMacrosExecution=true", pairs);
        Assert.Contains("BlockUntrustedRefererLinks=true", pairs);
        Assert.Contains("ooInetProxyType=2", pairs);
        Assert.Contains("ooInetHTTPProxyPort=9", pairs);
        Assert.Contains("ooInetNoProxy=", pairs);
    }

    [Fact]
    public void ProfileWithExternalResources_KeepsMacrosOff_AndRestrictsNothingElse()
    {
        string[] pairs = Pairs(LibreOfficeProfile.SettingsWithExternalResources);

        Assert.Contains("MacroSecurityLevel=3", pairs);
        Assert.Contains("DisableMacrosExecution=true", pairs);
        Assert.Equal(2, pairs.Length);
    }

    [Fact]
    public void Profile_IsChosenBySetting_AndStrictByDefault()
    {
        string strict = Path.Combine(_temp, "strict");
        string open = Path.Combine(_temp, "open");

        LibreOfficeProfile.Write(strict);
        LibreOfficeProfile.Write(open, allowExternalResources: true);

        Assert.Equal(LibreOfficeProfile.Settings, File.ReadAllText(Path.Combine(strict, "user", "registrymodifications.xcu")));
        Assert.Equal(LibreOfficeProfile.SettingsWithExternalResources, File.ReadAllText(Path.Combine(open, "user", "registrymodifications.xcu")));
        Assert.False(new OfficeSettings().AllowExternalResources);
    }

    [Fact]
    public void Extension_RejectsNullSettings()
    {
        Assert.Throws<ArgumentNullException>(() => new OfficeExtension(null!));
    }

    [Fact]
    public void Profile_IsWrittenIntoTheFolderOfTheRun_WithoutAByteOrderMark()
    {
        string folder = Path.Combine(_temp, "profile");

        LibreOfficeProfile.Write(folder);

        byte[] bytes = File.ReadAllBytes(Path.Combine(folder, "user", "registrymodifications.xcu"));
        Assert.Equal("<?xml"u8.ToArray(), bytes[..5]);
        Assert.Equal(LibreOfficeProfile.Settings, Encoding.UTF8.GetString(bytes));
    }

    [OfficeFact]
    public async Task HtmlWithAnOutsidePicture_DoesNotReachTheNetwork()
    {
        using CountingServer server = new();
        string html = Path.Combine(_temp, "внешний.html");
        await File.WriteAllTextAsync(html, $"""<html><head><meta charset="utf-8"></head><body><p>Внешняя картинка</p><img src="{server.Url("picture.png")}"></body></html>""", new UTF8Encoding(false));

        ConversionResult result = await Converter().ConvertAsync(Source.File(html), Target.Memory(FileFormat.Pdf));

        Assert.True(result.Succeeded);
        Assert.Equal(0, server.Connections);
    }

    [OfficeTheory]
    [InlineData("stylesheet", """<link rel="stylesheet" href="{0}">""", "<p>x</p>")]
    [InlineData("css-background", "", """<p style="background:url({0})">x</p>""")]
    public async Task HtmlWithAnOutsideStylesheetOrCssBackground_DoesNotReachTheNetwork(string name, string head, string body)
    {
        using CountingServer server = new();
        string html = Path.Combine(_temp, name + ".html");
        string url = server.Url("resource.css");
        await File.WriteAllTextAsync(html, $"""<html><head><meta charset="utf-8">{string.Format(head, url)}</head><body>{string.Format(body, url)}</body></html>""", new UTF8Encoding(false));

        ConversionResult result = await Converter().ConvertAsync(Source.File(html), Target.Memory(FileFormat.Pdf));

        Assert.True(result.Succeeded);
        Assert.Equal(0, server.Connections);
    }

    [OfficeFact]
    public async Task DocxWithALinkedPicture_DoesNotReachTheNetwork()
    {
        using CountingServer server = new();
        string docx = DocxWithLinkedPicture(server.Url("picture.png"));
        try
        {
            ConversionResult result = await Converter().ConvertAsync(Source.File(docx), Target.Memory(FileFormat.Pdf));

            Assert.True(result.Succeeded);
            Assert.Equal(0, server.Connections);
        }
        finally
        {
            File.Delete(docx);
        }
    }

    [OfficeFact]
    public async Task HtmlWithAnOutsidePicture_ReachesTheNetwork_WhenExternalResourcesAreAllowed()
    {
        using CountingServer server = new();
        string html = Path.Combine(_temp, "внешний-разрешён.html");
        await File.WriteAllTextAsync(html, $"""<html><head><meta charset="utf-8"></head><body><p>Внешняя картинка</p><img src="{server.Url("picture.png")}"></body></html>""", new UTF8Encoding(false));

        ConversionResult result = await Converter(new OfficeSettings { AllowExternalResources = true }).ConvertAsync(Source.File(html), Target.Memory(FileFormat.Pdf));

        Assert.True(result.Succeeded);
        Assert.True(server.Connections > 0, "The picture was not requested although outside resources are allowed.");
    }

    [OfficeFact]
    public async Task DocxWithALinkedPicture_ReachesTheNetwork_WhenExternalResourcesAreAllowed()
    {
        using CountingServer server = new();
        string docx = DocxWithLinkedPicture(server.Url("picture.png"));
        try
        {
            ConversionResult result = await Converter(new OfficeSettings { AllowExternalResources = true }).ConvertAsync(Source.File(docx), Target.Memory(FileFormat.Pdf));

            Assert.True(result.Succeeded);
            Assert.True(server.Connections > 0, "The linked picture was not requested although outside resources are allowed.");
        }
        finally
        {
            File.Delete(docx);
        }
    }

    [OfficeFact]
    public async Task HtmlWithAnEmbeddedPicture_StillShowsIt()
    {
        string html = Path.Combine(_temp, "встроенный.html");
        await File.WriteAllTextAsync(
            html,
            """<html><head><meta charset="utf-8"></head><body><p>Встроенная картинка</p><img width="40" height="40" src="data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg=="></body></html>""",
            new UTF8Encoding(false));

        ConversionResult result = await Converter().ConvertAsync(Source.File(html), Target.Memory(FileFormat.Pdf));

        using PdfDocument pdf = PdfDocument.Open(result.Bytes!);
        Assert.NotEmpty(pdf.GetPage(1).GetImages());
    }

    [OfficeTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DocumentWithAMacroOnLoad_DoesNotRunIt(bool allowExternalResources)
    {
        string marker = Path.Combine(_temp, "macro-ran.txt");
        string document = Path.Combine(_temp, "макрос.odt");
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Samples", "sample-macro.odt"), document);
        using (ZipArchive zip = ZipFile.Open(document, ZipArchiveMode.Update))
        {
            ZipArchiveEntry entry = zip.GetEntry("Basic/Standard/Module1.xml")!;
            string text;
            using (StreamReader reader = new(entry.Open(), Encoding.UTF8))
            {
                text = await reader.ReadToEndAsync();
            }

            Assert.Matches(MarkerCall(), text);
            entry.Delete();
            using StreamWriter writer = new(zip.CreateEntry("Basic/Standard/Module1.xml").Open(), new UTF8Encoding(false));
            await writer.WriteAsync(MarkerCall().Replace(text, $"ConvertToURL(&quot;{marker.Replace('\\', '/')}&quot;)"));
        }

        ConversionResult result = await Converter(new OfficeSettings { AllowExternalResources = allowExternalResources }).ConvertAsync(Source.File(document), Target.Memory(FileFormat.Pdf));

        Assert.True(result.Succeeded);
        Assert.False(File.Exists(marker), "The macro of the document ran.");
    }
}
