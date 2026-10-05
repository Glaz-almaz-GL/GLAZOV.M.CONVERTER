using GLAZOV.M.CONVERTER.Capabilities;
using GLAZOV.M.CONVERTER.Settings;
using GLAZOV.M.CONVERTER.Tooling;

namespace GLAZOV.M.CONVERTER.Tests.Tooling;

/// <summary>
/// Поиск программы: приоритет пути из настроек, PATH, привычные места, папка пакетов, подсказка по системе.
/// Окружение подставное — папки и «программы»-файлы создаются во временной папке; одна проверка — на настоящем FFmpeg.
/// </summary>
public sealed class ToolLocatorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "convtests-" + Guid.NewGuid().ToString("N"));

    public ToolLocatorTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        Directory.Delete(_root, recursive: true);
    }

    private static string FileNameOf(string tool)
    {
        return OperatingSystem.IsWindows() ? tool + ".exe" : tool;
    }

    private string Folder(string name)
    {
        string path = Path.Combine(_root, name);
        Directory.CreateDirectory(path);
        return path;
    }

    private static string MakeTool(string folder, string tool, bool executable = true)
    {
        string path = Path.Combine(folder, FileNameOf(tool));
        File.WriteAllBytes(path, [0]);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, executable ? UnixFileMode.UserRead | UnixFileMode.UserExecute : UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        return path;
    }

    private static ToolLocator Locator(ToolEnvironment environment, ConverterSettings? settings = null)
    {
        return new ToolLocator(ConverterSettingsSnapshot.Create(settings ?? new ConverterSettings()), environment);
    }

    private static ToolEnvironment Env(string? path = null, string[]? usual = null, string? packages = null)
    {
        return new ToolEnvironment(path, usual ?? [], packages);
    }

    [Fact]
    public void Ffprobe_IsFoundBesideTheFoundFfmpeg_EvenOutsidePath()
    {
        string folder = Folder("pkg-bin");
        MakeTool(folder, "ffmpeg");
        string probe = MakeTool(folder, "ffprobe");
        ConverterSettings settings = new();
        settings.ToolPaths["ffmpeg"] = Path.Combine(folder, FileNameOf("ffmpeg"));

        ToolStatus status = Locator(Env(), settings).Locate("ffprobe");

        Assert.True(status.Found);
        Assert.Equal(probe, status.Path);
    }

    [Fact]
    public void Ffprobe_PathFromSettings_WinsOverTheOneBesideFfmpeg()
    {
        string folder = Folder("pkg-bin");
        MakeTool(folder, "ffmpeg");
        MakeTool(folder, "ffprobe");
        string own = MakeTool(Folder("own"), "ffprobe");
        ConverterSettings settings = new();
        settings.ToolPaths["ffmpeg"] = Path.Combine(folder, FileNameOf("ffmpeg"));
        settings.ToolPaths["ffprobe"] = own;

        Assert.Equal(own, Locator(Env(), settings).Locate("ffprobe").Path);
    }

    [Fact]
    public void Ffprobe_IsNotFound_WhenFfmpegIsNotFoundAndNothingElseHasIt()
    {
        ToolStatus status = Locator(Env()).Locate("ffprobe");

        Assert.False(status.Found);
        Assert.Null(status.Path);
    }

    [Fact]
    public void PathFromSettings_WinsOverPath()
    {
        string inPath = MakeTool(Folder("pathdir"), "ffmpeg");
        string configured = MakeTool(Folder("custom"), "ffmpeg");
        ConverterSettings settings = new() { ToolPaths = { ["ffmpeg"] = configured } };

        ToolStatus status = Locator(Env(path: Path.GetDirectoryName(inPath)), settings).Locate("ffmpeg");

        Assert.True(status.Found);
        Assert.Equal(configured, status.Path);
    }

    [Fact]
    public void PathFromSettings_ThatDoesNotExist_IsNotFound_NoSilentFallbackToPath()
    {
        string inPath = MakeTool(Folder("pathdir"), "ffmpeg");
        string missing = Path.Combine(_root, "нет", FileNameOf("ffmpeg"));
        ConverterSettings settings = new() { ToolPaths = { ["ffmpeg"] = missing } };

        ToolStatus status = Locator(Env(path: Path.GetDirectoryName(inPath)), settings).Locate("ffmpeg");

        Assert.False(status.Found);
        Assert.Null(status.Path);
        Assert.Contains(missing, status.InstallHint);
        Assert.Contains("ToolPaths", status.InstallHint);
    }

    [Fact]
    public void FoundInPath()
    {
        string tool = MakeTool(Folder("bin"), "ffmpeg");

        ToolStatus status = Locator(Env(path: Path.GetDirectoryName(tool))).Locate("ffmpeg");

        Assert.True(status.Found);
        Assert.Equal(tool, status.Path);
        Assert.Equal("ffmpeg", status.Name);
    }

    [Fact]
    public void BrokenPathEntries_AreSkipped_SearchContinues()
    {
        string tool = MakeTool(Folder("good"), "ffmpeg");
        string path = string.Join(Path.PathSeparator, "", "   ", "\0плохо", Path.Combine(_root, "нет-такой"), Path.GetDirectoryName(tool));

        ToolStatus status = Locator(Env(path: path)).Locate("ffmpeg");

        Assert.True(status.Found);
        Assert.Equal(tool, status.Path);
    }

    [Fact]
    public void NotInPath_FoundInUsualFolder()
    {
        string tool = MakeTool(Folder("usual"), "ffmpeg");

        ToolStatus status = Locator(Env(path: Folder("empty"), usual: [Path.Combine(_root, "нет"), Path.GetDirectoryName(tool)!])).Locate("ffmpeg");

        Assert.True(status.Found);
        Assert.Equal(tool, status.Path);
    }

    [Fact]
    public void FoundInPackageFolder_OnlyInPackagesNamedLikeTheTool()
    {
        string packages = Folder("packages");
        string right = Folder(Path.Combine("packages", "Gyan.FFmpeg_abc", "ffmpeg-9", "bin"));
        string wrong = Folder(Path.Combine("packages", "Other.Pkg_abc", "bin"));
        string tool = MakeTool(right, "ffmpeg");
        MakeTool(wrong, "other-ffmpeg-copy");

        ToolStatus status = Locator(Env(packages: packages)).Locate("ffmpeg");
        ToolStatus notThere = Locator(Env(packages: packages)).Locate("magick");

        Assert.True(status.Found);
        Assert.Equal(tool, status.Path);
        Assert.False(notThere.Found);
    }

    [Fact]
    public void NotFoundAnywhere_GivesInstallHintForThisSystem()
    {
        ToolStatus status = Locator(Env(path: Folder("empty"))).Locate("ffmpeg");

        Assert.False(status.Found);
        Assert.Null(status.Path);
        string expected = OperatingSystem.IsWindows() ? "winget install Gyan.FFmpeg"
            : OperatingSystem.IsMacOS() ? "brew install ffmpeg"
            : "apt install ffmpeg";
        Assert.Contains(expected, status.InstallHint);
    }

    [Fact]
    public void UnknownTool_GetsAGenericHint()
    {
        ToolStatus status = Locator(Env()).Locate("магия");

        Assert.False(status.Found);
        Assert.Contains("«магия»", status.InstallHint);
    }

    [Fact]
    public void Result_IsCached_ForTheLifeOfTheLocator_AndNameIsCaseInsensitive()
    {
        string tool = MakeTool(Folder("bin"), "ffmpeg");
        ToolLocator locator = Locator(Env(path: Path.GetDirectoryName(tool)));

        ToolStatus first = locator.Locate("ffmpeg");
        File.Delete(tool);
        ToolStatus second = locator.Locate("FFMPEG");

        Assert.Same(first, second);
        Assert.True(second.Found);
    }

    [UnixFact]
    public void FileWithoutExecuteBit_IsNotAProgram()
    {
        string tool = MakeTool(Folder("bin"), "ffmpeg", executable: false);

        ToolStatus status = Locator(Env(path: Path.GetDirectoryName(tool))).Locate("ffmpeg");

        Assert.False(status.Found);
    }

    [Fact]
    public void EmptyName_IsAUsageError()
    {
        Assert.Throws<ArgumentException>(() => Locator(Env()).Locate(" "));
    }

    [Fact]
    public void Soffice_PathFromSettings_WinsOverPathAndUsualFolders()
    {
        string inPath = MakeTool(Folder("pathdir"), "soffice");
        string usual = MakeTool(Folder("usual"), "soffice");
        string configured = MakeTool(Folder("custom"), "soffice");
        ConverterSettings settings = new() { ToolPaths = { ["soffice"] = configured } };

        ToolStatus status = Locator(Env(path: Path.GetDirectoryName(inPath), usual: [Path.GetDirectoryName(usual)!]), settings).Locate("soffice");

        Assert.Equal(configured, status.Path);
    }

    [Fact]
    public void Soffice_PathFromSettings_ThatDoesNotExist_IsNotFound_NoSilentFallback()
    {
        string inPath = MakeTool(Folder("pathdir"), "soffice");
        string missing = Path.Combine(_root, "нет", FileNameOf("soffice"));
        ConverterSettings settings = new() { ToolPaths = { ["soffice"] = missing } };

        ToolStatus status = Locator(Env(path: Path.GetDirectoryName(inPath)), settings).Locate("soffice");

        Assert.False(status.Found);
        Assert.Null(status.Path);
        Assert.Contains(missing, status.InstallHint);
    }

    [Fact]
    public void Soffice_IsFoundInPath_BeforeUsualFolders()
    {
        string inPath = MakeTool(Folder("pathdir"), "soffice");
        string usual = MakeTool(Folder("usual"), "soffice");

        ToolStatus status = Locator(Env(path: Path.GetDirectoryName(inPath), usual: [Path.GetDirectoryName(usual)!])).Locate("soffice");

        Assert.Equal(inPath, status.Path);
    }

    [Fact]
    public void Soffice_NotInPath_IsFoundInUsualFolder()
    {
        string tool = MakeTool(Folder("usual"), "soffice");

        ToolStatus status = Locator(Env(path: Folder("empty"), usual: [Path.GetDirectoryName(tool)!])).Locate("soffice");

        Assert.Equal(tool, status.Path);
    }

    [Fact]
    public void Soffice_UnderItsAlias_IsFound_InPathAndInUsualFolders_WhenTheSystemHasAnAlias()
    {
        IReadOnlyDictionary<string, string[]> aliases = new Dictionary<string, string[]> { ["soffice"] = ["libreoffice"] };
        string inPath = MakeTool(Folder("pathdir"), "libreoffice");
        string usual = MakeTool(Folder("usual"), "libreoffice");

        ToolStatus byPath = Locator(new ToolEnvironment(Path.GetDirectoryName(inPath), [], null, aliases)).Locate("soffice");
        ToolStatus byUsual = Locator(new ToolEnvironment(Folder("empty"), [Path.GetDirectoryName(usual)!], null, aliases)).Locate("soffice");
        ToolStatus withoutAlias = Locator(Env(path: Path.GetDirectoryName(inPath))).Locate("soffice");

        Assert.Equal(inPath, byPath.Path);
        Assert.Equal(usual, byUsual.Path);
        Assert.False(withoutAlias.Found);
    }

    [Fact]
    public void Soffice_OwnName_WinsOverItsAlias_InTheSameFolder()
    {
        IReadOnlyDictionary<string, string[]> aliases = new Dictionary<string, string[]> { ["soffice"] = ["libreoffice"] };
        string folder = Folder("bin");
        MakeTool(folder, "libreoffice");
        string own = MakeTool(folder, "soffice");

        Assert.Equal(own, Locator(new ToolEnvironment(folder, [], null, aliases)).Locate("soffice").Path);
    }

    [Fact]
    public void Soffice_IsFoundInPackageFolder_NamedAfterTheSuite()
    {
        string packages = Folder("packages");
        string right = Folder(Path.Combine("packages", "TheDocumentFoundation.LibreOffice_abc", "program"));
        string tool = MakeTool(right, "soffice");
        MakeTool(Folder(Path.Combine("packages", "Other.Pkg_abc", "bin")), "soffice");

        ToolStatus status = Locator(Env(packages: packages)).Locate("soffice");

        Assert.Equal(tool, status.Path);
    }

    [Fact]
    public void ExpandFolders_GivesExistingMatches_InNameOrder_AndNothingForAMissingParent()
    {
        string parent = Folder("opt");
        Folder(Path.Combine("opt", "libreoffice26.8"));
        Folder(Path.Combine("opt", "libreoffice7.6"));
        Folder(Path.Combine("opt", "other"));

        string[] expanded = ToolEnvironment.ExpandFolders(parent, "libreoffice", "program").ToArray();

        Assert.Equal([Path.Combine(parent, "libreoffice26.8", "program"), Path.Combine(parent, "libreoffice7.6", "program")], expanded);
        Assert.Empty(ToolEnvironment.ExpandFolders(Path.Combine(_root, "нет"), "libreoffice", "program"));
    }

    [Fact]
    public void ThisSystem_KnowsTheUsualPlacesOfLibreOffice()
    {
        ToolEnvironment environment = ToolEnvironment.ForThisSystem();

        if (OperatingSystem.IsWindows())
        {
            Assert.Contains(environment.UsualFolders, folder => folder.EndsWith(Path.Combine("LibreOffice", "program"), StringComparison.OrdinalIgnoreCase));
        }
        else if (OperatingSystem.IsMacOS())
        {
            Assert.Contains("/Applications/LibreOffice.app/Contents/MacOS", environment.UsualFolders);
        }
        else
        {
            Assert.Contains("/usr/bin", environment.UsualFolders);
            Assert.Contains("libreoffice", environment.Aliases!["soffice"]);
        }
    }

    [Fact]
    public void Soffice_NotFoundAnywhere_GivesInstallHintForThisSystem()
    {
        ToolStatus status = Locator(Env(path: Folder("empty"))).Locate("soffice");

        Assert.False(status.Found);
        string expected = OperatingSystem.IsWindows() ? "winget install TheDocumentFoundation.LibreOffice"
            : OperatingSystem.IsMacOS() ? "brew install --cask libreoffice"
            : "apt install libreoffice-core libreoffice-writer libreoffice-calc libreoffice-impress";
        Assert.Contains(expected, status.InstallHint);
        Assert.Contains("ToolPaths", status.InstallHint);
    }

    // Only found, never started: soffice.exe --version opens a console window that waits for Enter.
    [ExternalFact]
    public void RealSoffice_IsFound()
    {
        ToolLocator locator = new(ConverterSettingsSnapshot.Create(new ConverterSettings()));

        ToolStatus status = locator.Locate("soffice");

        Assert.True(status.Found);
        Assert.True(File.Exists(status.Path));
    }

    [ExternalFact]
    public async Task RealFfmpeg_IsFound_AndRuns()
    {
        ToolLocator locator = new(ConverterSettingsSnapshot.Create(new ConverterSettings()));

        ToolStatus status = locator.Locate("ffmpeg");
        ToolResult result = await ToolRunner.RunAsync(new ToolInvocation(status.Path!, ["-version"]));

        Assert.True(status.Found);
        Assert.True(File.Exists(status.Path));
        Assert.Equal(0, result.ExitCode);
    }
}
