using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Errors;
using GLAZOV.M.CONVERTER.Extensions;
using GLAZOV.M.CONVERTER.Formats;
using GLAZOV.M.CONVERTER.Tooling;

namespace GLAZOV.M.CONVERTER.Ffmpeg.Engines;

/// <summary>
/// Изображения через FFmpeg (решение D-010): входы PNG, JPEG, GIF, WebP, BMP, TIFF, DDS, PSD, AVIF, ICO; результаты PNG, JPEG, WebP, BMP, GIF, TIFF, TGA, AVIF, ICO.
/// Вход и результат идут через временные файлы: TIFF, DDS и PSD из канала не читаются, а результат всегда должен быть целым.
/// У многокадровых входов (GIF, анимированный AVIF) берётся первый кадр, у ICO с несколькими картинками — самая большая.
/// </summary>
internal sealed class FfmpegImageEngine : IConversionEngine
{
    private static readonly FileFormat[] Inputs =
    [
        FileFormat.Png, FileFormat.Jpeg, FileFormat.Gif, FileFormat.Webp, FileFormat.Bmp, FileFormat.Tiff, FileFormat.Dds, FileFormat.Psd, FileFormat.Avif, FileFormat.Ico,
    ];

    private static readonly FileFormat[] Outputs =
    [
        FileFormat.Png, FileFormat.Jpeg, FileFormat.Webp, FileFormat.Bmp, FileFormat.Gif, FileFormat.Tiff, FileFormat.Tga, FileFormat.Avif, FileFormat.Ico,
    ];

    private readonly FfmpegSettings _settings;

    /// <summary>
    /// Creates the engine.
    /// </summary>
    /// <param name="settings">Defaults for quality and background; <c>null</c> — the default settings.</param>
    public FfmpegImageEngine(FfmpegSettings? settings = null)
    {
        _settings = settings ?? new FfmpegSettings();
    }

    /// <inheritdoc />
    public IReadOnlyList<ToolSpec> Tools { get; } = [FfmpegTools.Ffmpeg];

    /// <inheritdoc />
    public bool SupportsOperations => true;

    /// <inheritdoc />
    public IReadOnlyList<ConversionPair> Pairs { get; } =
        [.. Inputs.SelectMany(from => Outputs.Select(to => new ConversionPair(from, to)))];

    /// <inheritdoc />
    public bool CanConvert(FileFormat from, FileFormat to)
    {
        return Inputs.Contains(from) && Outputs.Contains(to);
    }

    /// <inheritdoc />
    public async Task ConvertAsync(ConversionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        string stem = Path.Combine(request.TempFolder, $"convert-{Guid.NewGuid():N}");
        string inputPath = $"{stem}.in.{request.From.Extension}";
        string outputPath = $"{stem}.out.{request.To.Extension}";

        try
        {
            await using (FileStream input = new(inputPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous))
            {
                await request.Input.CopyToAsync(input, cancellationToken).ConfigureAwait(false);
            }

            IReadOnlyList<string> filters = FfmpegImageArguments.Filters(request.Options.Operations);
            ToolResult result = null!;
            foreach (string? encoder in request.To.Equals(FileFormat.Avif) ? (IEnumerable<string?>)FfmpegImageArguments.Av1Encoders : new string?[] { null })
            {
                IReadOnlyList<string> args = FfmpegImageArguments.Build(
                    request.To, request.Options.Image?.Quality, inputPath, outputPath, filters, _settings, encoder);
                result = await ToolRunner.RunAsync(new ToolInvocation(request.Tool!.Path!, args), cancellationToken).ConfigureAwait(false);
                if (result.ExitCode == 0 || !IsMissingEncoder(result.ErrorOutput))
                {
                    break;
                }
            }

            if (result.ExitCode != 0)
            {
                if (request.To.Equals(FileFormat.Ico) && result.ErrorOutput.Contains("dimensions cannot exceed", StringComparison.Ordinal))
                {
                    throw ConversionException.InvalidParameters("Изображение больше 256×256 пикселей, а значок ICO больше не бывает: уменьшите его действием масштаба.");
                }

                throw ConversionException.ToolFailed(request.Tool!.Name, result.ExitCode, Explain(request.To, result.ErrorOutput));
            }

            await using FileStream produced = new(outputPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
            await produced.CopyToAsync(request.Output, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            File.Delete(inputPath);
            File.Delete(outputPath);
        }
    }

    private static bool IsMissingEncoder(string output)
    {
        return output.Contains("Unknown encoder", StringComparison.Ordinal);
    }

    // The last line of the output becomes the message, so the plain words go last.
    private static string Explain(FileFormat to, string output)
    {
        if (to.Equals(FileFormat.Avif) && IsMissingEncoder(output))
        {
            return $"{output}\nваш FFmpeg собран без кодировщика AV1 ({string.Join(", ", FfmpegImageArguments.Av1Encoders)}), AVIF записать нельзя";
        }

        return output;
    }
}
