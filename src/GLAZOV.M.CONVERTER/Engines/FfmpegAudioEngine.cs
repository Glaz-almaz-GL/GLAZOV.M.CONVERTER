using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Errors;
using GLAZOV.M.CONVERTER.Tooling;

namespace GLAZOV.M.CONVERTER.Engines;

/// <summary>
/// Звук между MP3, WAV и OGG через FFmpeg. MP3 и OGG идут каналами (вход — стандартный ввод, выход — стандартный вывод);
/// WAV пишется во временный файл, потому что FFmpeg правит размеры в заголовке WAV, перематывая выход, а канал этого не даёт.
/// Результат OGG — Vorbis; вход Opus принимается.
/// </summary>
internal sealed class FfmpegAudioEngine : IConversionEngine
{
    private static readonly FileFormat[] Supported = [FileFormat.Mp3, FileFormat.Wav, FileFormat.Ogg];

    /// <inheritdoc />
    public string ToolName => "ffmpeg";

    /// <inheritdoc />
    public IReadOnlyList<(FileFormat From, FileFormat To)> Pairs { get; } =
        [.. Supported.SelectMany(from => Supported.Select(to => (from, to)))];

    /// <inheritdoc />
    public bool CanConvert(FileFormat from, FileFormat to)
    {
        return Supported.Contains(from) && Supported.Contains(to);
    }

    /// <inheritdoc />
    public async Task ConvertAsync(EngineRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        bool needsTempFile = request.To.Equals(FileFormat.Wav);
        string? tempPath = needsTempFile
            ? Path.Combine(request.TempFolder, $"convert-{Guid.NewGuid():N}.wav")
            : null;

        try
        {
            IReadOnlyList<string> args = FfmpegAudioArguments.Build(request.To, request.Audio, tempPath ?? "pipe:1");
            ToolInvocation invocation = new(
                request.Tool.Path!,
                args,
                StandardInput: request.Input,
                StandardOutput: needsTempFile ? null : request.Output);

            ToolResult result = await ToolRunner.RunAsync(invocation, cancellationToken).ConfigureAwait(false);
            if (result.ExitCode != 0)
            {
                throw Failures.ToolFailed(ToolName, result.ExitCode, result.ErrorOutput);
            }

            if (tempPath is not null)
            {
                await using FileStream produced = new(tempPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
                await produced.CopyToAsync(request.Output, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            if (tempPath is not null && File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }
}
