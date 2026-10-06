using GLAZOV.M.CONVERTER.Office.Engines;

namespace GLAZOV.M.CONVERTER.Office.Tests.Engines;

/// <summary>
/// Arguments of a LibreOffice run: headless, own profile per run, result to the given folder.
/// </summary>
public sealed class LibreOfficeArgumentsTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "папка с пробелом");

    [Fact]
    public void Run_IsHeadless_ConvertsToPdf_IntoTheGivenFolder()
    {
        string input = Path.Combine(Root, "in", "отчёт 1.docx");
        string output = Path.Combine(Root, "out");

        IReadOnlyList<string> args = LibreOfficeArguments.ToPdf(input, output, Path.Combine(Root, "profile"));

        Assert.Contains("--headless", args);
        Assert.Contains("--norestore", args);
        Assert.Equal(["--convert-to", "pdf"], args.Skip(args.ToList().IndexOf("--convert-to")).Take(2));
        Assert.Equal(output, args[args.ToList().IndexOf("--outdir") + 1]);
        Assert.Equal(input, args[^1]);
    }

    [Fact]
    public void Profile_IsAFileUrl_OfTheGivenFolder_WithSpacesAndLettersEscaped()
    {
        string profile = Path.Combine(Root, "profile");

        string argument = LibreOfficeArguments.ToPdf("a.docx", "out", profile).Single(a => a.StartsWith("-env:UserInstallation=", StringComparison.Ordinal));

        string url = argument["-env:UserInstallation=".Length..];
        Assert.StartsWith("file:///", url);
        Assert.DoesNotContain(' ', url);
        Assert.Equal(Path.GetFullPath(profile), new Uri(url).LocalPath.Replace('/', Path.DirectorySeparatorChar));
    }

    [Fact]
    public void DifferentRuns_GetDifferentProfiles()
    {
        string first = LibreOfficeArguments.ToPdf("a.docx", "out", Path.Combine(Root, "p1"))[0];
        string second = LibreOfficeArguments.ToPdf("a.docx", "out", Path.Combine(Root, "p2"))[0];

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void EmptyPaths_AreAUsageError()
    {
        Assert.Throws<ArgumentException>(() => LibreOfficeArguments.ToPdf(" ", "o", "p"));
        Assert.Throws<ArgumentException>(() => LibreOfficeArguments.ToPdf("i", "", "p"));
        Assert.Throws<ArgumentException>(() => LibreOfficeArguments.ToPdf("i", "o", " "));
    }

    [Fact]
    public void Invocation_TakesTheStandardOutput_SoNothingReachesTheConsoleOfTheHost()
    {
        GLAZOV.M.CONVERTER.Tooling.ToolInvocation invocation = LibreOfficeEngine.Invocation("soffice", "a.docx", "out", "profile");

        Assert.NotNull(invocation.OnOutputLine);
        Assert.Null(invocation.StandardOutput);
        invocation.OnOutputLine("convert a.docx -> out/a.pdf");
    }
}
