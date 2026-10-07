using System.Text.RegularExpressions;

namespace GLAZOV.M.CONVERTER.Composition.Tests;

/// <summary>
/// The core library knows no format and no program: a search through its sources, comments left out, finds neither,
/// and its project refers to no other project.
/// </summary>
public sealed partial class CoreIsEmptyOfFormatsTests
{
    private const string Values = "Mp3|Wav|Ogg|Flac|M4a|Jpeg|Png|Webp|Bmp|Gif|Tiff|Tga|Dds|Psd|Avif|Ico|Svg|Mp4|Mkv|Webm|Mov|Avi|Pdf|Docx|Doc|Odt|Rtf|Xlsx|Xls|Ods|Pptx|Ppt|Odp|Html|Text";

    [GeneratedRegex(@"\bFileFormat\.(" + Values + @")\b")]
    private static partial Regex FormatValue();

    [GeneratedRegex(@"ffmpeg|ffprobe|soffice", RegexOptions.IgnoreCase)]
    private static partial Regex ProgramName();

    [GeneratedRegex(@"(^|\s)//.*$")]
    private static partial Regex LineComment();

    private static string CoreFolder()
    {
        for (DirectoryInfo? folder = new(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            string core = Path.Combine(folder.FullName, "src", "GLAZOV.M.CONVERTER");
            if (File.Exists(Path.Combine(core, "GLAZOV.M.CONVERTER.csproj")))
            {
                return core;
            }
        }

        throw new DirectoryNotFoundException("The source folder of the core library is not found above the test folder.");
    }

    private static IEnumerable<(string File, int Line, string Code)> CodeLines()
    {
        string core = CoreFolder();
        foreach (string file in Directory.EnumerateFiles(core, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            {
                continue;
            }

            string[] lines = File.ReadAllLines(file);
            for (int index = 0; index < lines.Length; index++)
            {
                yield return (Path.GetRelativePath(core, file), index + 1, LineComment().Replace(lines[index], string.Empty));
            }
        }
    }

    [Fact]
    public void TheSourcesOfTheCore_CountSomeCodeLines()
    {
        Assert.True(CodeLines().Count() > 1000, "the search must see the sources, or it proves nothing");
    }

    [Fact]
    public void NoNamedFormatValue_IsReadInTheCore()
    {
        string[] found = [.. CodeLines().Where(line => FormatValue().IsMatch(line.Code)).Select(line => $"{line.File}:{line.Line}")];

        Assert.Empty(found);
    }

    [Fact]
    public void NoProgramName_IsWrittenInTheCoreCode()
    {
        string[] found = [.. CodeLines().Where(line => ProgramName().IsMatch(line.Code)).Select(line => $"{line.File}:{line.Line}")];

        Assert.Empty(found);
    }

    [Fact]
    public void TheCoreProject_ReferencesNoOtherProject()
    {
        string project = File.ReadAllText(Path.Combine(CoreFolder(), "GLAZOV.M.CONVERTER.csproj"));

        Assert.DoesNotContain("ProjectReference", project);
    }

    [Fact]
    public void TheCoreFolder_HasNoFileOfTheRemovedKinds()
    {
        string[] names = [.. Directory.EnumerateFiles(CoreFolder(), "*.cs", SearchOption.AllDirectories).Select(Path.GetFileName)!];

        Assert.DoesNotContain(names, name => name is "SignatureMatcher.cs" or "BuiltInExtension.cs" or "FileTypeDetector.cs" or "IUsesProbe.cs");
        Assert.DoesNotContain(names, name => name.StartsWith("LibreOffice", StringComparison.Ordinal) || name.StartsWith("Ffmpeg", StringComparison.Ordinal));
    }
}
