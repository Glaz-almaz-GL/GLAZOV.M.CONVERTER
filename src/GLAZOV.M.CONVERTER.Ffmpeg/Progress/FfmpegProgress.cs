using System.Globalization;
using System.Text.RegularExpressions;

namespace GLAZOV.M.CONVERTER.Ffmpeg.Progress;

/// <summary>
/// Turns FFmpeg output lines into a growing percent: the input duration comes from the "Duration:" banner line,
/// the position from the <c>out_time_us</c> lines of <c>-progress</c>. Format-independent; thread-safe,
/// so the standard error and the standard output may feed it from different threads.
/// </summary>
internal sealed partial class FfmpegProgress
{
    private const string PositionKey = "out_time_us=";

    private readonly Action<int> _onPercent;
    private readonly object _lock = new();
    private long? _durationUs;
    private bool _durationSeen;
    private int _last;

    internal FfmpegProgress(Action<int> onPercent)
    {
        _onPercent = onPercent;
    }

    [GeneratedRegex(@"Duration:\s*(?:(\d+):(\d{2}):(\d{2})(?:\.(\d+))?|N/A)")]
    private static partial Regex DurationPattern();

    /// <summary>
    /// Feeds one line of FFmpeg output; reports 1–99 when the percent has grown.
    /// </summary>
    /// <param name="line">A line from the standard error or the standard output.</param>
    internal void ProcessLine(string line)
    {
        ArgumentNullException.ThrowIfNull(line);

        lock (_lock)
        {
            if (line.StartsWith(PositionKey, StringComparison.Ordinal))
            {
                ProcessPosition(line[PositionKey.Length..]);
            }
            else if (!_durationSeen)
            {
                ProcessDuration(line);
            }
        }
    }

    private void ProcessDuration(string line)
    {
        Match match = DurationPattern().Match(line);
        if (!match.Success)
        {
            return;
        }

        _durationSeen = true;
        if (!match.Groups[1].Success)
        {
            return;
        }

        double seconds = (long.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) * 3600)
            + (long.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) * 60)
            + long.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
        if (match.Groups[4].Success)
        {
            seconds += double.Parse("0." + match.Groups[4].Value, CultureInfo.InvariantCulture);
        }

        long micro = (long)(seconds * 1_000_000);
        _durationUs = micro > 0 ? micro : null;
    }

    // N/A, junk and negative positions (FFmpeg prints them before the first frame) are not positions.
    private void ProcessPosition(string value)
    {
        if (_durationUs is not { } duration || !long.TryParse(value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out long position))
        {
            return;
        }

        int percent = (int)Math.Min(99, position * 100.0 / duration);
        if (percent <= _last)
        {
            return;
        }

        _last = percent;
        _onPercent(percent);
    }
}
