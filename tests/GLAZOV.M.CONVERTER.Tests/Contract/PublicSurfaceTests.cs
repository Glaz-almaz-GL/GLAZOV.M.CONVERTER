using System.Reflection;
using GLAZOV.M.CONVERTER.Batch;
using GLAZOV.M.CONVERTER.Capabilities;
using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Detection;
using GLAZOV.M.CONVERTER.Errors;
using GLAZOV.M.CONVERTER.Settings;

namespace GLAZOV.M.CONVERTER.Tests.Contract;

/// <summary>
/// Публичная поверхность библиотеки равна принятому эталону: ни одного лишнего типа и члена (приёмка контракта).
/// Список ниже — это эталон, переписанный в имена; менять его можно только вместе с эталоном и решением владельца.
/// </summary>
public class PublicSurfaceTests
{
    private static readonly Dictionary<string, string[]> Expected = new()
    {
        ["AudioOptions"] = ["Bitrate", "Channels", "SampleRate"],
        ["BatchItemResult"] = ["Canceled", "Message", "OutputPath", "Reason", "SourceName", "Stage", "Succeeded"],
        ["BatchProgress"] = ["Completed", "CurrentName", "Total"],
        ["BatchReport"] = ["Canceled", "FailedSources", "Failed", "Items", "Succeeded", "Total"],
        ["Conversion"] = ["Availability", "From", "To"],
        ["ConversionAvailability"] = ["Ready", "ToolMissing"],
        ["ConversionException"] = ["Reason", "Stage", "ToolExitCode", "ToolName", "ToolOutput"],
        ["ConversionFailureReason"] = ["FileExists", "InputUnreadable", "ToolFailed", "ToolNotFound", "TypeNotConfirmed", "UnsupportedConversion"],
        ["ConversionOptions"] = ["Audio", "Operations"],
        ["ConversionProgress"] = ["Percent"],
        ["ConversionResult"] = ["Bytes", "Duration", "OutputPath", "SourceType", "Stream", "Succeeded", "TargetFormat"],
        ["ConversionStage"] = ["Conversion", "Detection", "Preparation"],
        ["Converter"] = [".ctor", "Capabilities", "ConvertAsync", "ConvertBatchAsync"],
        ["ConverterCapabilities"] = ["All", "CanConvert", "Tool"],
        ["ConverterSettings"] = ["MaxParallelTasks", "OverwriteExisting", "TempFolder", "ToolPaths"],
        ["FileType"] = ["Extension", "MimeType", "Mp3", "Name", "Ogg", "Unknown", "Wav"],
        ["FileTypeDetector"] = ["DetectAsync"],
        ["Format"] = ["Jpeg", "Mp3", "Ogg", "Wav"],
        ["Operation"] = ["RotateRight", "ScalePercent"],
        ["Source"] = ["File", "Folder", "Stream"],
        ["Target"] = ["File", "Folder", "Memory", "Stream"],
        ["ToolStatus"] = ["Found", "InstallHint", "Name", "Path"],
    };

    [Fact]
    public void ExportedTypes_AreExactlyTheSketchTypes()
    {
        string[] actual = typeof(Converter).Assembly.GetExportedTypes().Select(t => t.Name).Order().ToArray();

        Assert.Equal(Expected.Keys.Order().ToArray(), actual);
    }

    [Theory]
    [MemberData(nameof(TypeNames))]
    public void PublicMembers_AreExactlyTheSketchMembers(string typeName)
    {
        Type type = typeof(Converter).Assembly.GetExportedTypes().Single(t => t.Name == typeName);
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        var actual = type.GetMembers(flags)
            .Where(m => !(m is MethodBase method && method.IsSpecialName && m.Name != ".ctor"))
            .Where(m => m.Name != ".ctor" || type == typeof(Converter))
            .Select(m => m.Name)
            .Where(n => !(type.IsEnum && n == "value__"))
            .Where(n => n != "ToString")
            .Distinct()
            .Order()
            .ToArray();

        Assert.Equal(Expected[typeName].Order().ToArray(), actual);
    }

    public static IEnumerable<object[]> TypeNames()
    {
        return Expected.Keys.Select(k => new object[] { k });
    }
}
