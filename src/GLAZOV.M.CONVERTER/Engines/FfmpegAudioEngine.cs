using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Errors;
using GLAZOV.M.CONVERTER.Tooling;

namespace GLAZOV.M.CONVERTER.Engines;

/// <summary>
/// Звук между MP3, WAV, OGG, FLAC и M4A через FFmpeg. OGG идёт каналами (вход — стандартный ввод, выход — стандартный вывод);
/// Остальные форматы пишутся во временный файл, потому что FFmpeg после записи возвращается в начало выхода: в WAV правит размеры заголовка, в MP3 пишет служебный заголовок с длительностью (Xing/Info), во FLAC — число отсчётов и контрольную сумму, в M4A переносит индекс (faststart) — канал этого не даёт.
/// Результат OGG — Vorbis; вход Opus принимается.
/// </summary>
internal sealed class FfmpegAudioEngine : IConversionEngine
{
    private static readonly FileFormat[] Supported = [FileFormat.Mp3, FileFormat.Wav, FileFormat.Ogg, FileFormat.Flac, FileFormat.M4a];

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

        bool needsTempFile = !request.To.Equals(FileFormat.Ogg);
        string? tempPath = needsTempFile
            ? Path.Combine(request.TempFolder, $"convert-{Guid.NewGuid():N}.{request.To.Extension}")
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
