using System.Diagnostics;

namespace GLAZOV.M.CONVERTER.Archives.Tests;

/// <summary>
/// A check that needs the program "tar" of the machine (it reads TAR, TAR.GZ and ZIP). Without it the check is skipped with the reason,
/// so that a skipped check is told from a passed one.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class TarToolFactAttribute : FactAttribute
{
    public TarToolFactAttribute()
    {
        if (!TarTool.Available)
        {
            Skip = "The program tar is not found on this machine. The live check was not run (neither passed nor failed).";
        }
    }
}

/// <summary>
/// Runs the program "tar" of the machine.
/// </summary>
internal static class TarTool
{
    internal static bool Available { get; } = Run("--version", out _) == 0;

    /// <summary>
    /// Lists an archive with <c>tar -tf</c>; returns the exit code.
    /// </summary>
    /// <param name="path">The archive.</param>
    /// <param name="listing">What tar printed.</param>
    internal static int List(string path, out string listing)
    {
        return Run($"-tf \"{path}\"", out listing);
    }

    private static int Run(string arguments, out string output)
    {
        try
        {
            using Process process = Process.Start(new ProcessStartInfo("tar", arguments)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                UseShellExecute = false,
                CreateNoWindow = true,
            })!;
            output = process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            output = string.Empty;
            return -1;
        }
    }
}
