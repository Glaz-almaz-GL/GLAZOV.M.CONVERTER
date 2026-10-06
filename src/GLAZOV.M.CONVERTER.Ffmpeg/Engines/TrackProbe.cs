using System.Text;
using System.Text.Json;
using GLAZOV.M.CONVERTER.Errors;
using GLAZOV.M.CONVERTER.Tooling;

namespace GLAZOV.M.CONVERTER.Ffmpeg.Engines;

/// <summary>
/// Reads the track codecs of a file with ffprobe. Any trouble (program missing, file unreadable, odd output)
/// gives <c>null</c>: the caller then encodes, which is right but slower.
/// </summary>
internal static class TrackProbe
{
    /// <summary>
    /// Returns the codecs of the first video and first audio track, or <c>null</c> when they cannot be read.
    /// </summary>
    /// <param name="probePath">Path of ffprobe.</param>
    /// <param name="file">File to read.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    internal static async Task<TrackCodecs?> ReadAsync(string probePath, string file, CancellationToken cancellationToken)
    {
        using MemoryStream output = new();
        try
        {
            ToolResult result = await ToolRunner.RunAsync(
                new ToolInvocation(
                    probePath,
                    ["-v", "error", "-show_entries", "stream=codec_type,codec_name,pix_fmt", "-of", "json", file],
                    StandardOutput: output),
                cancellationToken).ConfigureAwait(false);
            return result.ExitCode == 0 ? Parse(Encoding.UTF8.GetString(output.ToArray())) : null;
        }
        catch (ConversionException)
        {
            return null;
        }
    }

    /// <summary>
    /// Takes the first video and the first audio codec out of the JSON of <c>ffprobe -of json</c>; <c>null</c> for other text.
    /// </summary>
    /// <param name="json">Output of ffprobe.</param>
    internal static TrackCodecs? Parse(string json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("streams", out JsonElement streams) || streams.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            string? video = null;
            string? audio = null;
            string? pixelFormat = null;
            foreach (JsonElement stream in streams.EnumerateArray())
            {
                string? type = Text(stream, "codec_type");
                string? name = Text(stream, "codec_name");
                if (type == "video" && video is null)
                {
                    video = name ?? "unknown";
                    pixelFormat = Text(stream, "pix_fmt");
                }
                else if (type == "audio" && audio is null)
                {
                    audio = name ?? "unknown";
                }
            }

            return new TrackCodecs(video, audio, pixelFormat);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Text(JsonElement element, string name)
    {
        return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }
}
