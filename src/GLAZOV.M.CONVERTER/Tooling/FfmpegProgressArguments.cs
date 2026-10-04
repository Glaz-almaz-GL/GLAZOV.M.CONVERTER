namespace GLAZOV.M.CONVERTER.Tooling;

/// <summary>
/// Adds the options that make FFmpeg print machine-readable progress for <see cref="FfmpegProgress"/>.
/// </summary>
internal static class FfmpegProgressArguments
{
    /// <summary>
    /// Returns the arguments with <c>-progress &lt;target&gt; -nostats</c> in front. The "Duration:" line the percent
    /// needs is printed at the default log level; a level below "info" hides it and no percent is reported.
    /// </summary>
    /// <param name="arguments">FFmpeg arguments.</param>
    /// <param name="target">Where FFmpeg writes progress: <c>pipe:1</c> (standard output) or <c>pipe:2</c> (standard error).</param>
    internal static IReadOnlyList<string> Add(IReadOnlyList<string> arguments, string target = "pipe:1")
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentException.ThrowIfNullOrWhiteSpace(target);

        return ["-progress", target, "-nostats", .. arguments];
    }
}
