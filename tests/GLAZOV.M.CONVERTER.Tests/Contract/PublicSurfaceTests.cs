using GLAZOV.M.CONVERTER.Converting;
using System.Reflection;

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
        ["ConversionFailureReason"] = ["EngineFailed", "FileExists", "InputUnreadable", "OutputUnwritable", "ToolFailed", "ToolNotFound", "TypeNotConfirmed", "UnsupportedConversion"],
        ["ConversionPair"] = ["Deconstruct", "From", "To"],
        ["ConversionRequest"] = ["From", "Input", "InputPath", "Options", "Output", "ReportProgress", "TempFolder", "To", "Tool"],
        ["IConversionEngine"] = ["ConvertAsync", "Pairs", "SupportsOperations", "ToolName"],
        ["IConverterExtension"] = ["Detectors", "Engines", "Name"],
        ["IFormatDetector"] = ["Detect", "HeadLength"],
        ["ConversionOptions"] = ["Audio", "Image", "Operations", "Video"],
        ["ImageOptions"] = ["Quality"],
        ["ConversionProgress"] = ["Percent"],
        ["ConversionResult"] = ["Bytes", "Duration", "OutputPath", "Reencoded", "SourceType", "Stream", "Succeeded", "TargetFormat"],
        ["ConversionStage"] = ["Conversion", "Detection", "Preparation"],
        ["Converter"] = [".ctor", "Capabilities", "ConvertAsync", "ConvertBatchAsync"],
        ["ConverterCapabilities"] = ["All", "CanConvert", "Tool"],
        ["ConverterSettings"] = ["DetectionWindow", "Extensions", "MaxParallelTasks", "OverwriteExisting", "TempFolder", "ToolPaths"],
        ["FileTypeDetector"] = ["DetectAsync"],
        ["FileFormat"] = ["Avi", "Bmp", "Create", "Dds", "Extension", "Flac", "Gif", "Jpeg", "M4a", "MimeType", "Mkv", "Mov", "Mp3", "Mp4", "Name", "Ogg", "Png", "Psd", "Svg", "Tga", "Tiff", "Unknown", "Wav", "Webm", "Webp"],
        ["Operation"] = ["Kind", "Percent", "RotateRight", "ScalePercent"],
        ["OperationKind"] = ["RotateRight", "ScalePercent"],
        ["Source"] = ["File", "Folder", "Stream"],
        ["Target"] = ["File", "Folder", "Memory", "Stream"],
        ["VideoOptions"] = ["Bitrate", "ConvertPixelFormat", "FrameRate", "Height", "Quality", "Reencode", "Width"],
        ["ToolStatus"] = ["Found", "InstallHint", "Name", "Path"],
    };

    [Fact]
    public void ExportedTypes_AreExactlyTheSketchTypes()
    {
        string[] actual = [.. typeof(Converter).Assembly.GetExportedTypes().Select(t => t.Name).Order()];

        Assert.Equal(Expected.Keys.Order().ToArray(), actual);
    }

    [Theory]
    [MemberData(nameof(TypeNames))]
    public void PublicMembers_AreExactlyTheSketchMembers(string typeName)
    {
        Type type = typeof(Converter).Assembly.GetExportedTypes().Single(t => t.Name == typeName);
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        string[] actual = [.. type.GetMembers(flags)
            .Where(m => !(m is MethodBase method && method.IsSpecialName && m.Name != ".ctor"))
            .Where(m => m.Name != ".ctor" || type == typeof(Converter))
            .Select(m => m.Name)
            .Where(n => !(type.IsEnum && n == "value__"))
            .Where(n => n is not ("ToString" or "Equals" or "GetHashCode"))
            .Distinct()
            .Order()];

        Assert.Equal(Expected[typeName].Order().ToArray(), actual);
    }

    public static IEnumerable<object[]> TypeNames()
    {
        return Expected.Keys.Select(k => new object[] { k });
    }
}
