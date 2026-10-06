using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Extensions;
using GLAZOV.M.CONVERTER.Formats;
using GLAZOV.M.CONVERTER.Pdf.Rendering;

namespace GLAZOV.M.CONVERTER.Pdf.Engines;

/// <summary>
/// Picture-to-PDF engine: PNG, JPEG, WebP, BMP and GIF (first frame) become a PDF of one page
/// as large as the picture. Runs in the process on SkiaSharp, with no external program.
/// </summary>
internal sealed class ImagePdfEngine : IConversionEngine
{
    private readonly PdfLimits _limits;

    /// <summary>
    /// Creates the engine.
    /// </summary>
    /// <param name="limits">Copy of the settings.</param>
    internal ImagePdfEngine(PdfLimits limits)
    {
        _limits = limits;
    }

    /// <inheritdoc />
    public string? ToolName => null;

    /// <inheritdoc />
    public bool SupportsOperations => false;

    /// <inheritdoc />
    public IReadOnlyList<ConversionPair> Pairs { get; } =
    [
        new(FileFormat.Png, FileFormat.Pdf),
        new(FileFormat.Jpeg, FileFormat.Pdf),
        new(FileFormat.Webp, FileFormat.Pdf),
        new(FileFormat.Bmp, FileFormat.Pdf),
        new(FileFormat.Gif, FileFormat.Pdf),
    ];

    /// <inheritdoc />
    public async Task ConvertAsync(ConversionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        byte[] picture = await ReadLimitedAsync(request.Input, _limits.MaxInputBytes, cancellationToken).ConfigureAwait(false);
        byte[] pdf = ImagePdfWriter.Write(picture, _limits, cancellationToken);
        await request.Output.WriteAsync(pdf, cancellationToken).ConfigureAwait(false);
    }

    // The picture is decoded in memory, so the read stops at the limit.
    private static async Task<byte[]> ReadLimitedAsync(Stream input, long maxBytes, CancellationToken cancellationToken)
    {
        using MemoryStream buffer = new();
        byte[] chunk = new byte[81920];
        int read;
        while ((read = await input.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + read > maxBytes)
            {
                throw new InvalidDataException($"The picture is larger than the limit of {maxBytes} bytes (setting PdfSettings.MaxInputBytes).");
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }
}
