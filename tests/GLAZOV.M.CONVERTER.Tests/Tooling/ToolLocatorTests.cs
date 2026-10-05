using GLAZOV.M.CONVERTER.Capabilities;
using GLAZOV.M.CONVERTER.Engines;
using GLAZOV.M.CONVERTER.Extensions;
using GLAZOV.M.CONVERTER.Settings;
using GLAZOV.M.CONVERTER.Tooling;

namespace GLAZOV.M.CONVERTER.Tests.Tooling;

/// <summary>
/// Поиск программы по описанию: приоритет пути из настроек, PATH, места поиска из описания, папка пакетов, папка другой программы, подсказка по системе.
/// Окружение подставное — папки и «программы»-файлы создаются во временной папке; описания готовых движков берутся копиями без мест поиска,
/// чтобы настоящие папки машины не влияли на результат; две проверки — на настоящих FFmpeg и LibreOffice.
/// </summary>
public sealed class ToolLocatorTests : IDisposable
{
    private static readonly ToolSystem ThisSystem = new ToolEnvironment(null, null).System;

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

    // A copy of a ready description with the places of this system replaced: the real folders of the machine must not decide the result.
    private static ToolSpec Bare(ToolSpec original, params string[] places)
    {
        ToolSpec copy = new(original.Name)
        {
            Aliases = original.Aliases,
            Required = original.Required,
            FoundBeside = original.FoundBeside,
            PackageWord = original.PackageWord,
        };
        foreach ((ToolSystem system, string hint) in original.InstallHints)
        {
            copy.InstallHints[system] = hint;
        }

        copy.SearchPlaces[ThisSystem] = places;
        return copy;
    }

    private static ToolSpec Ffmpeg(params string[] places) => Bare(FfmpegTools.Ffmpeg, places);

    private static ToolSpec Ffprobe() => Bare(FfmpegTools.Ffprobe);

    private static ToolSpec Soffice(params string[] places) => Bare(LibreOfficeTools.Soffice, places);

    private static ToolSpec[] Siblings() => [Ffmpeg(), Ffprobe()];

    private static ToolLocator Locator(ToolEnvironment environment, ConverterSettings? settings = null)
    {
        return new ToolLocator(ConverterSettingsSnapshot.Create(settings ?? new ConverterSettings()), environment);
    }

    private static ToolEnvironment Env(string? path = null, string? packages = null)
    {
        return new ToolEnvironment(path, packages);
    }

    [Fact]
    public void Ffprobe_IsFoundBesideTheFoundFfmpeg_EvenOutsidePath()
    {
        string folder = Folder("pkg-bin");
        MakeTool(folder, "ffmpeg");
        string probe = MakeTool(folder, "ffprobe");
        ConverterSettings settings = new();
        settings.ToolPaths["ffmpeg"] = Path.Combine(folder, FileNameOf("ffmpeg"));

        ToolStatus status = Locator(Env(), settings).Locate(Ffprobe(), Siblings());

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

        Assert.Equal(own, Locator(Env(), settings).Locate(Ffprobe(), Siblings()).Path);
    }

    [Fact]
    public void Ffprobe_IsNotFound_WhenFfmpegIsNotFoundAndNothingElseHasIt()
    {
        ToolStatus status = Locator(Env()).Locate(Ffprobe(), Siblings());

        Assert.False(status.Found);
        Assert.Null(status.Path);
    }

    [Fact]
    public void ABesideProgramThatIsNotAmongTheSiblings_IsNotLookedFor()
    {
        string folder = Folder("pkg-bin");
        MakeTool(folder, "ffmpeg");
        MakeTool(folder, "ffprobe");
        ConverterSettings settings = new();
        settings.ToolPaths["ffmpeg"] = Path.Combine(folder, FileNameOf("ffmpeg"));

        Assert.False(Locator(Env(), settings).Locate(Ffprobe()).Found);
    }

    [Fact]
    public void PathFromSettings_WinsOverPath()
    {
        string inPath = MakeTool(Folder("pathdir"), "ffmpeg");
        string configured = MakeTool(Folder("custom"), "ffmpeg");
        ConverterSettings settings = new() { ToolPaths = { ["ffmpeg"] = configured } };

        ToolStatus status = Locator(Env(path: Path.GetDirectoryName(inPath)), settings).Locate(Ffmpeg());

        Assert.True(status.Found);
        Assert.Equal(configured, status.Path);
    }

    [Fact]
    public void PathFromSettings_ThatDoesNotExist_IsNotFound_NoSilentFallbackToPath()
    {
        string inPath = MakeTool(Folder("pathdir"), "ffmpeg");
        string missing = Path.Combine(_root, "нет", FileNameOf("ffmpeg"));
        ConverterSettings settings = new() { ToolPaths = { ["ffmpeg"] = missing } };

        ToolStatus status = Locator(Env(path: Path.GetDirectoryName(inPath)), settings).Locate(Ffmpeg());

        Assert.False(status.Found);
        Assert.Null(status.Path);
        Assert.Contains(missing, status.InstallHint);
        Assert.Contains("ToolPaths", status.InstallHint);
    }

    [Fact]
    public void FoundInPath()
    {
        string tool = MakeTool(Folder("bin"), "ffmpeg");

        ToolStatus status = Locator(Env(path: Path.GetDirectoryName(tool))).Locate(Ffmpeg());

        Assert.True(status.Found);
        Assert.Equal(tool, status.Path);
        Assert.Equal("ffmpeg", status.Name);
    }

    [Fact]
    public void BrokenPathEntries_AreSkipped_SearchContinues()
    {
        string tool = MakeTool(Folder("good"), "ffmpeg");
        string path = string.Join(Path.PathSeparator, "", "   ", "\0плохо", Path.Combine(_root, "нет-такой"), Path.GetDirectoryName(tool));

        ToolStatus status = Locator(Env(path: path)).Locate(Ffmpeg());

        Assert.True(status.Found);
        Assert.Equal(tool, status.Path);
    }

    [Fact]
    public void NotInPath_FoundInAPlaceOfTheDescription()
    {
        string tool = MakeTool(Folder("usual"), "ffmpeg");

        ToolStatus status = Locator(Env(path: Folder("empty"))).Locate(Ffmpeg(Path.Combine(_root, "нет"), Path.GetDirectoryName(tool)!));

        Assert.True(status.Found);
        Assert.Equal(tool, status.Path);
    }

    [Fact]
    public void PlacesOfAnotherSystem_AreNotSearched()
    {
        string tool = MakeTool(Folder("usual"), "ffmpeg");
        ToolSystem other = ThisSystem == ToolSystem.Linux ? ToolSystem.Windows : ToolSystem.Linux;
        ToolSpec spec = Ffmpeg();
        spec.SearchPlaces[other] = [Path.GetDirectoryName(tool)!];

        Assert.False(Locator(Env(path: Folder("empty"))).Locate(spec).Found);
    }

    [Fact]
    public void FoundInPackageFolder_OnlyInPackagesNamedLikeTheTool()
    {
        string packages = Folder("packages");
        string right = Folder(Path.Combine("packages", "Gyan.FFmpeg_abc", "ffmpeg-9", "bin"));
        string wrong = Folder(Path.Combine("packages", "Other.Pkg_abc", "bin"));
        string tool = MakeTool(right, "ffmpeg");
        MakeTool(wrong, "other-ffmpeg-copy");

        ToolStatus status = Locator(Env(packages: packages)).Locate(Ffmpeg());
        ToolStatus notThere = Locator(Env(packages: packages)).Locate(new ToolSpec("magick"));

        Assert.True(status.Found);
        Assert.Equal(tool, status.Path);
        Assert.False(notThere.Found);
    }

    [Fact]
    public void NotFoundAnywhere_GivesInstallHintForThisSystem()
    {
        ToolStatus status = Locator(Env(path: Folder("empty"))).Locate(Ffmpeg());

        Assert.False(status.Found);
        Assert.Null(status.Path);
        string expected = OperatingSystem.IsWindows() ? "winget install Gyan.FFmpeg"
            : OperatingSystem.IsMacOS() ? "brew install ffmpeg"
            : "apt install ffmpeg";
        Assert.Contains(expected, status.InstallHint);
    }

    [Fact]
    public void ADescriptionWithoutHints_GetsAGenericHint()
    {
        ToolStatus status = Locator(Env()).Locate(new ToolSpec("магия"));

        Assert.False(status.Found);
        Assert.Contains("«магия»", status.InstallHint);
    }

    [Fact]
    public void Result_IsCached_ForTheLifeOfTheLocator_AndNameIsCaseInsensitive()
    {
        string tool = MakeTool(Folder("bin"), "ffmpeg");
        ToolLocator locator = Locator(Env(path: Path.GetDirectoryName(tool)));

        ToolStatus first = locator.Locate(Ffmpeg());
        File.Delete(tool);
        ToolStatus second = locator.Locate(new ToolSpec("FFMPEG"));

        Assert.Same(first, second);
        Assert.True(second.Found);
    }

    [UnixFact]
    public void FileWithoutExecuteBit_IsNotAProgram()
    {
        string tool = MakeTool(Folder("bin"), "ffmpeg", executable: false);

        ToolStatus status = Locator(Env(path: Path.GetDirectoryName(tool))).Locate(Ffmpeg());

        Assert.False(status.Found);
    }

    [Fact]
    public void EmptyName_IsAUsageError()
    {
        Assert.Throws<ArgumentException>(() => new ToolSpec(" "));
    }

    [Fact]
    public void Soffice_PathFromSettings_WinsOverPathAndPlaces()
    {
        string inPath = MakeTool(Folder("pathdir"), "soffice");
        string usual = MakeTool(Folder("usual"), "soffice");
        string configured = MakeTool(Folder("custom"), "soffice");
        ConverterSettings settings = new() { ToolPaths = { ["soffice"] = configured } };

        ToolStatus status = Locator(Env(path: Path.GetDirectoryName(inPath)), settings).Locate(Soffice(Path.GetDirectoryName(usual)!));

        Assert.Equal(configured, status.Path);
    }

    [Fact]
    public void Soffice_PathFromSettings_ThatDoesNotExist_IsNotFound_NoSilentFallback()
    {
        string inPath = MakeTool(Folder("pathdir"), "soffice");
        string missing = Path.Combine(_root, "нет", FileNameOf("soffice"));
        ConverterSettings settings = new() { ToolPaths = { ["soffice"] = missing } };

        ToolStatus status = Locator(Env(path: Path.GetDirectoryName(inPath)), settings).Locate(Soffice());

        Assert.False(status.Found);
        Assert.Null(status.Path);
        Assert.Contains(missing, status.InstallHint);
    }

    [Fact]
    public void Soffice_IsFoundInPath_BeforePlaces()
    {
        string inPath = MakeTool(Folder("pathdir"), "soffice");
        string usual = MakeTool(Folder("usual"), "soffice");

        ToolStatus status = Locator(Env(path: Path.GetDirectoryName(inPath))).Locate(Soffice(Path.GetDirectoryName(usual)!));

        Assert.Equal(inPath, status.Path);
    }

    [Fact]
    public void Soffice_NotInPath_IsFoundInAPlace()
    {
        string tool = MakeTool(Folder("usual"), "soffice");

        ToolStatus status = Locator(Env(path: Folder("empty"))).Locate(Soffice(Path.GetDirectoryName(tool)!));

        Assert.Equal(tool, status.Path);
    }

    [Fact]
    public void Soffice_UnderItsAlias_IsFound_InPathAndInPlaces()
    {
        string inPath = MakeTool(Folder("pathdir"), "libreoffice");
        string usual = MakeTool(Folder("usual"), "libreoffice");
        ToolSpec withoutAlias = new("soffice");

        ToolStatus byPath = Locator(Env(path: Path.GetDirectoryName(inPath))).Locate(Soffice());
        ToolStatus byPlace = Locator(Env(path: Folder("empty"))).Locate(Soffice(Path.GetDirectoryName(usual)!));
        ToolStatus notWithoutAlias = Locator(Env(path: Path.GetDirectoryName(inPath))).Locate(withoutAlias);

        Assert.Equal(inPath, byPath.Path);
        Assert.Equal(usual, byPlace.Path);
        Assert.False(notWithoutAlias.Found);
    }

    [Fact]
    public void Soffice_OwnName_WinsOverItsAlias_InTheSameFolder()
    {
        string folder = Folder("bin");
        MakeTool(folder, "libreoffice");
        string own = MakeTool(folder, "soffice");

        Assert.Equal(own, Locator(Env(path: folder)).Locate(Soffice()).Path);
    }

    [Fact]
    public void Soffice_IsFoundInPackageFolder_NamedAfterTheSuite()
    {
        string packages = Folder("packages");
        string right = Folder(Path.Combine("packages", "TheDocumentFoundation.LibreOffice_abc", "program"));
        string tool = MakeTool(right, "soffice");
        MakeTool(Folder(Path.Combine("packages", "Other.Pkg_abc", "bin")), "soffice");

        ToolStatus status = Locator(Env(packages: packages)).Locate(Soffice());

        Assert.Equal(tool, status.Path);
    }

    [Fact]
    public void ExpandPlace_GivesExistingMatches_InNameOrder_AndNothingForAMissingParent()
    {
        string parent = Folder("opt");
        Folder(Path.Combine("opt", "libreoffice26.8"));
        Folder(Path.Combine("opt", "libreoffice7.6"));
        Folder(Path.Combine("opt", "other"));
        string sep = Path.DirectorySeparatorChar.ToString();

        string[] expanded = ToolEnvironment.ExpandPlace(parent + sep + "libreoffice*" + sep + "program").ToArray();

        Assert.Equal([Path.Combine(parent, "libreoffice26.8", "program"), Path.Combine(parent, "libreoffice7.6", "program")], expanded);
        Assert.Empty(ToolEnvironment.ExpandPlace(Path.Combine(_root, "нет") + sep + "libreoffice*" + sep + "program"));
        Assert.Equal([parent], ToolEnvironment.ExpandPlace(parent));
    }

    [Fact]
    public void ExpandPlace_ExpandsEnvironmentVariables()
    {
        Environment.SetEnvironmentVariable("CONVTEST_PLACE", _root);
        try
        {
            Assert.Equal([_root], ToolEnvironment.ExpandPlace("%CONVTEST_PLACE%").Concat(ToolEnvironment.ExpandPlace(_root)).Distinct());
        }
        finally
        {
            Environment.SetEnvironmentVariable("CONVTEST_PLACE", null);
        }
    }

    [Fact]
    public void ReadyDescriptions_KnowTheUsualPlacesOfTheirPrograms()
    {
        Assert.Contains(FfmpegTools.Ffmpeg.SearchPlaces[ToolSystem.Windows], place => place.Contains("WinGet", StringComparison.Ordinal));
        Assert.Contains(FfmpegTools.Ffmpeg.SearchPlaces[ToolSystem.MacOS], place => place == "/opt/homebrew/bin");
        Assert.Contains(FfmpegTools.Ffmpeg.SearchPlaces[ToolSystem.Linux], place => place == "/usr/bin");
        Assert.Contains(LibreOfficeTools.Soffice.SearchPlaces[ToolSystem.Windows], place => place.EndsWith(Path.Combine("LibreOffice", "program"), StringComparison.OrdinalIgnoreCase));
        Assert.Contains("/Applications/LibreOffice.app/Contents/MacOS", LibreOfficeTools.Soffice.SearchPlaces[ToolSystem.MacOS]);
        Assert.Contains("/opt/libreoffice*/program", LibreOfficeTools.Soffice.SearchPlaces[ToolSystem.Linux]);
        Assert.Contains("libreoffice", LibreOfficeTools.Soffice.Aliases);
    }

    [Fact]
    public void Soffice_NotFoundAnywhere_GivesInstallHintForThisSystem()
    {
        ToolStatus status = Locator(Env(path: Folder("empty"))).Locate(Soffice());

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

        ToolStatus status = locator.Locate(LibreOfficeTools.Soffice);

        Assert.True(status.Found);
        Assert.True(File.Exists(status.Path));
    }

    [ExternalFact]
    public async Task RealFfmpeg_IsFound_AndRuns()
    {
        ToolLocator locator = new(ConverterSettingsSnapshot.Create(new ConverterSettings()));

        ToolStatus status = locator.Locate(FfmpegTools.Ffmpeg);
        ToolResult result = await ToolRunner.RunAsync(new ToolInvocation(status.Path!, ["-version"]));

        Assert.True(status.Found);
        Assert.True(File.Exists(status.Path));
        Assert.Equal(0, result.ExitCode);
    }
}
