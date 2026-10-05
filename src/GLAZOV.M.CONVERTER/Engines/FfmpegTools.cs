using GLAZOV.M.CONVERTER.Extensions;

namespace GLAZOV.M.CONVERTER.Engines;

/// <summary>
/// Descriptions of the FFmpeg programs the built-in engines need.
/// </summary>
internal static class FfmpegTools
{
    private static readonly IReadOnlyList<string> WindowsPlaces =
    [
        @"%ProgramFiles%\ffmpeg\bin",
        @"%ProgramFiles(x86)%\ffmpeg\bin",
        @"%LocalAppData%\Microsoft\WinGet\Links",
        @"%LocalAppData%\Programs\ffmpeg\bin",
        @"C:\ffmpeg\bin",
    ];

    private static readonly IReadOnlyList<string> MacPlaces = ["/opt/homebrew/bin", "/usr/local/bin", "/opt/local/bin"];
    private static readonly IReadOnlyList<string> LinuxPlaces = ["/usr/bin", "/usr/local/bin", "/snap/bin", "/opt/ffmpeg/bin"];

    /// <summary>
    /// FFmpeg, the program that converts.
    /// </summary>
    internal static ToolSpec Ffmpeg { get; } = Create(
        new ToolSpec("ffmpeg"),
        "Установите: winget install Gyan.FFmpeg (затем перезапустите программу) либо укажите путь в настройках (ToolPaths).",
        "Установите: brew install ffmpeg либо укажите путь в настройках (ToolPaths).",
        "Установите: sudo apt install ffmpeg (или пакет ffmpeg вашего дистрибутива) либо укажите путь в настройках (ToolPaths).");

    /// <summary>
    /// FFprobe, which reads the tracks of a file; it only improves the work (copying instead of re-encoding), so it is not required.
    /// </summary>
    internal static ToolSpec Ffprobe { get; } = Create(new ToolSpec("ffprobe") { Required = false, FoundBeside = "ffmpeg" }, null, null, null);

    private static ToolSpec Create(ToolSpec spec, string? windowsHint, string? macHint, string? linuxHint)
    {
        spec.SearchPlaces[ToolSystem.Windows] = WindowsPlaces;
        spec.SearchPlaces[ToolSystem.MacOS] = MacPlaces;
        spec.SearchPlaces[ToolSystem.Linux] = LinuxPlaces;
        if (windowsHint is not null && macHint is not null && linuxHint is not null)
        {
            spec.InstallHints[ToolSystem.Windows] = windowsHint;
            spec.InstallHints[ToolSystem.MacOS] = macHint;
            spec.InstallHints[ToolSystem.Linux] = linuxHint;
        }

        return spec;
    }
}
