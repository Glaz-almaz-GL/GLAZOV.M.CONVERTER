using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Extensions;
using GLAZOV.M.CONVERTER.Svg.Rendering;

namespace GLAZOV.M.CONVERTER.Svg.Engines;

/// <summary>
/// Движок SVG: SVG → PNG, JPEG, WebP растеризацией в процессе (SkiaSharp, Svg.Skia), без внешних программ.
/// Поворот и масштаб — перерисовкой вектора, без потери качества.
/// </summary>
internal sealed class SvgEngine : IConversionEngine
{
    private readonly RasterLimits _limits;

    /// <summary>
    /// Создаёт движок с пределами и умолчаниями.
    /// </summary>
    /// <param name="limits">Копия настроек движка.</param>
    internal SvgEngine(RasterLimits limits)
    {
        _limits = limits;
    }

    /// <inheritdoc />
    public string? ToolName => null;

    /// <inheritdoc />
    public bool SupportsOperations => true;

    /// <inheritdoc />
    public IReadOnlyList<ConversionPair> Pairs { get; } =
    [
        new(FileFormat.Svg, FileFormat.Png),
        new(FileFormat.Svg, FileFormat.Jpeg),
        new(FileFormat.Svg, FileFormat.Webp),
    ];

    /// <inheritdoc />
    public async Task ConvertAsync(ConversionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        byte[] svg = await ReadLimitedAsync(request.Input, _limits.MaxInputBytes, cancellationToken).ConfigureAwait(false);
        byte[] image = SvgRasterizer.Rasterize(svg, request.To, request.Options, _limits, cancellationToken);
        await request.Output.WriteAsync(image, cancellationToken).ConfigureAwait(false);
    }

    // Вход читается целиком, но не больше предела: документ разбирается в памяти.
    private static async Task<byte[]> ReadLimitedAsync(Stream input, long maxBytes, CancellationToken cancellationToken)
    {
        using MemoryStream buffer = new();
        byte[] chunk = new byte[81920];
        int read;
        while ((read = await input.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + read > maxBytes)
            {
                throw new InvalidDataException(
                    $"Документ SVG больше предела {maxBytes} байт (настройка SvgSettings.MaxInputBytes).");
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }
}
