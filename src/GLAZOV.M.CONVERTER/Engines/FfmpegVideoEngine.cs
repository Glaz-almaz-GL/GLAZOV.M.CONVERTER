using System.Text.RegularExpressions;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Errors;
using GLAZOV.M.CONVERTER.Extensions;
using GLAZOV.M.CONVERTER.IO;
using GLAZOV.M.CONVERTER.Tooling;

namespace GLAZOV.M.CONVERTER.Engines;

/// <summary>
/// Video between MP4, MKV, WebM, MOV and AVI through FFmpeg. A track is copied as it is when nothing asks to change it
/// and its codec fits the container (ffprobe tells the codecs), otherwise it is re-encoded. The input is read as a file
/// (the index of MP4, MOV and AVI may sit at the end), the result goes to a temp file first (MP4 and MOV write their
/// index back to the start) and is then copied to the output. The first video and the first audio track are taken;
/// subtitles, attachments and other tracks are dropped. Progress comes from FFmpeg's own output.
/// </summary>
internal sealed partial class FfmpegVideoEngine : IConversionEngine, IUsesProbe
{
    private static readonly FileFormat[] Supported = [FileFormat.Mp4, FileFormat.Mkv, FileFormat.Webm, FileFormat.Mov, FileFormat.Avi];

    /// <inheritdoc />
    public string ToolName => "ffmpeg";

    /// <inheritdoc />
    public bool SupportsOperations => true;

    /// <inheritdoc />
    public IReadOnlyList<ConversionPair> Pairs { get; } =
        [.. Supported.SelectMany(from => Supported.Select(to => new ConversionPair(from, to)))];

    [GeneratedRegex(@"Unknown encoder '([^']+)'")]
    private static partial Regex UnknownEncoderPattern();

    /// <inheritdoc />
    public async Task ConvertAsync(ConversionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        string tempPath = Path.Combine(request.TempFolder, $"convert-{Guid.NewGuid():N}.{request.To.Extension}");
        try
        {
            await using InputFile input = await InputFile.EnsureAsync(request, cancellationToken).ConfigureAwait(false);
            FfmpegProgress progress = new(request.ReportProgress);

            TrackCodecs? codecs = request.Probe is { Found: true, Path: { } probePath }
                ? await TrackProbe.ReadAsync(probePath, input.Path, cancellationToken).ConfigureAwait(false)
                : null;
            CopyPlan plan = CopyPlan.Decide(request.To, request.Options.Video, request.Options.Audio, request.Options.Operations.Count > 0, codecs);

            ToolResult result = await RunAsync(request, input.Path, tempPath, plan, codecs, progress, cancellationToken).ConfigureAwait(false);
            if (result.ExitCode != 0 && (plan.CopyVideo || plan.CopyAudio))
            {
                // The container refused a copied track: the table is a safe list, not a promise. Encode instead.
                plan = CopyPlan.EncodeAll;
                result = await RunAsync(request, input.Path, tempPath, plan, codecs, progress, cancellationToken).ConfigureAwait(false);
            }

            if (result.ExitCode != 0)
            {
                throw Failures.ToolFailed(ToolName, result.ExitCode, ExplainMissingEncoder(result.ErrorOutput));
            }

            request.Reencoded = !plan.CopiesEverything(codecs);
            await using FileStream produced = new(tempPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
            await produced.CopyToAsync(request.Output, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static Task<ToolResult> RunAsync(
        ConversionRequest request,
        string inputPath,
        string outputPath,
        CopyPlan plan,
        TrackCodecs? codecs,
        FfmpegProgress progress,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<string> args = FfmpegProgressArguments.Add(
            FfmpegVideoArguments.Build(request.To, request.Options.Video, request.Options.Audio, inputPath, outputPath, plan, codecs, request.Options.Operations));
        ToolInvocation invocation = new(
            request.Tool!.Path!,
            args,
            OnErrorLine: progress.ProcessLine,
            OnOutputLine: progress.ProcessLine);

        return ToolRunner.RunAsync(invocation, cancellationToken);
    }

    // A build of FFmpeg without an encoder says only "Unknown encoder"; the last line becomes the message, so the hint goes last.
    private static string ExplainMissingEncoder(string output)
    {
        Match match = UnknownEncoderPattern().Match(output);
        return match.Success ? $"{output}\nваш FFmpeg собран без кодека {match.Groups[1].Value}" : output;
    }
}
