using System.Diagnostics;

namespace GLAZOV.M.CONVERTER.ArchivesExtra.Tests;

/// <summary>
/// A check that needs bsdtar (the "tar" of Windows 10 and later, libarchive), the only program on the usual machine that writes 7z.
/// Without it the check is skipped with the reason, so that a skipped check is told from a passed one.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class BsdTarFactAttribute : FactAttribute
{
    public BsdTarFactAttribute()
    {
        if (BsdTar.Path is null)
        {
            Skip = "bsdtar is not found on this machine. The live check was not run (neither passed nor failed).";
        }
    }
}

/// <summary>
/// Runs bsdtar.
/// </summary>
internal static class BsdTar
{
    internal static string? Path { get; } = Find();

    /// <summary>
    /// Runs bsdtar with the arguments in a folder; returns the exit code.
    /// </summary>
    /// <param name="folder">Working folder.</param>
    /// <param name="arguments">Arguments.</param>
    internal static int Run(string folder, params string[] arguments)
    {
        ProcessStartInfo start = new(Path!) { WorkingDirectory = folder, UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(start)!;
        process.StandardOutput.ReadToEnd();
        process.StandardError.ReadToEnd();
        process.WaitForExit();
        return process.ExitCode;
    }

    private static string? Find()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        string path = System.IO.Path.Combine(Environment.SystemDirectory, "tar.exe");
        return File.Exists(path) ? path : null;
    }
}
