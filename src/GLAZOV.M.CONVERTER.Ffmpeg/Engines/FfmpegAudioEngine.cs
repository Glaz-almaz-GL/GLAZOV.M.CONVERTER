using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Errors;
using GLAZOV.M.CONVERTER.Extensions;
using GLAZOV.M.CONVERTER.Ffmpeg.Progress;
using GLAZOV.M.CONVERTER.IO;
using GLAZOV.M.CONVERTER.Tooling;

namespace GLAZOV.M.CONVERTER.Ffmpeg.Engines;

/// <summary>
/// Звук между MP3, WAV, OGG, FLAC и M4A и звук из видео (MP4, MKV, WebM, MOV, AVI) через FFmpeg. OGG из звука идёт каналами (вход — стандартный ввод, выход — стандартный вывод);
/// остальные форматы пишутся во временный файл, потому что FFmpeg после записи возвращается в начало выхода: в WAV правит размеры заголовка, в MP3 пишет служебный заголовок с длительностью (Xing/Info), во FLAC — число отсчётов и контрольную сумму, в M4A переносит индекс (faststart) — канал этого не даёт.
/// Видео читается файлом (индекс MP4, MOV и AVI может стоять в конце, из канала такой файл не прочитать), результат пишется во временный файл, ход работы идёт процентами.
/// Результат OGG — Vorbis; вход Opus принимается.
/// </summary>
internal sealed class FfmpegAudioEngine : IConversionEngine
{
    private static readonly FileFormat[] Supported = [FileFormat.Mp3, FileFormat.Wav, FileFormat.Ogg, FileFormat.Flac, FileFormat.M4a];
    private static readonly FileFormat[] VideoInputs = [FileFormat.Mp4, FileFormat.Mkv, FileFormat.Webm, FileFormat.Mov, FileFormat.Avi];

    private readonly FfmpegSettings _settings;

    /// <summary>
    /// Creates the engine.
    /// </summary>
    /// <param name="settings">Defaults for quality; <c>null</c> — the default settings.</param>
    public FfmpegAudioEngine(FfmpegSettings? settings = null)
    {
        _settings = settings ?? new FfmpegSettings();
    }

    /// <inheritdoc />
    public IReadOnlyList<ToolSpec> Tools { get; } = [FfmpegTools.Ffmpeg];

    /// <inheritdoc />
    public bool SupportsOperations => false;

    /// <inheritdoc />
    public IReadOnlyList<ConversionPair> Pairs { get; } =
        [.. Supported.Concat(VideoInputs).SelectMany(from => Supported.Select(to => new ConversionPair(from, to)))];

    /// <inheritdoc />
    public bool CanConvert(FileFormat from, FileFormat to)
    {
        return (Supported.Contains(from) || VideoInputs.Contains(from)) && Supported.Contains(to);
    }

    /// <inheritdoc />
    public async Task ConvertAsync(ConversionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        bool fromVideo = VideoInputs.Contains(request.From);
        bool needsTempFile = fromVideo || !request.To.Equals(FileFormat.Ogg);
        string? tempPath = needsTempFile
            ? Path.Combine(request.TempFolder, $"convert-{Guid.NewGuid():N}.{request.To.Extension}")
            : null;

        try
        {
            await using InputFile? inputFile = fromVideo ? await request.EnsureInputFileAsync(cancellationToken).ConfigureAwait(false) : null;
            ToolResult result = fromVideo
                ? await RunVideoAsync(request, inputFile!.Path, tempPath!, cancellationToken).ConfigureAwait(false)
                : await RunAudioAsync(request, tempPath, cancellationToken).ConfigureAwait(false);
            if (result.ExitCode != 0)
            {
                throw ConversionException.ToolFailed(request.Tool!.Name, result.ExitCode, fromVideo ? ExplainMissingSound(result.ErrorOutput) : result.ErrorOutput);
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

    private Task<ToolResult> RunAudioAsync(ConversionRequest request, string? tempPath, CancellationToken cancellationToken)
    {
        bool toStream = tempPath is null;
        IReadOnlyList<string> args = FfmpegAudioArguments.Build(request.To, request.Options.Audio, tempPath ?? "pipe:1", settings: _settings);
        ToolInvocation invocation = new(
            request.Tool!.Path!,
            args,
            StandardInput: request.Input,
            StandardOutput: toStream ? request.Output : null);

        return ToolRunner.RunAsync(invocation, cancellationToken);
    }

    // Video goes in as a file and out to a temp file; FFmpeg's own progress lines arrive on the standard output.
    private Task<ToolResult> RunVideoAsync(ConversionRequest request, string inputPath, string tempPath, CancellationToken cancellationToken)
    {
        FfmpegProgress progress = new(request.ReportProgress);
        IReadOnlyList<string> args = FfmpegProgressArguments.Add(
            FfmpegAudioArguments.Build(request.To, request.Options.Audio, tempPath, inputPath, fromVideo: true, settings: _settings));
        ToolInvocation invocation = new(
            request.Tool!.Path!,
            args,
            OnErrorLine: progress.ProcessLine,
            OnOutputLine: progress.ProcessLine);

        return ToolRunner.RunAsync(invocation, cancellationToken);
    }

    // FFmpeg says only "matches no streams"; for a video input the only mapped track is the sound, so the plain words go last.
    private static string ExplainMissingSound(string output)
    {
        return output.Contains("matches no streams", StringComparison.Ordinal) ? $"{output}\nв файле нет звуковой дорожки" : output;
    }
}
