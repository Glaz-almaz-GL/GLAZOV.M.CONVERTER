using System.Xml;
using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using SkiaSharp;
using Svg;
using Svg.Model;
using Svg.Skia;

namespace GLAZOV.M.CONVERTER.Svg.Rendering;

/// <summary>
/// Растеризация SVG: безопасная загрузка документа, перерисовка по плану, кодирование в PNG, JPEG или WebP.
/// </summary>
internal static class SvgRasterizer
{
    // Безопасная загрузка: без сценариев, без внешних файлов и адресов сети; разрешены только ссылки
    // внутри документа и встроенные данные (data:). Без этого SVG мог бы прочитать любой файл машины.
    private static readonly SvgDocumentLoadOptions SecureLoad = new()
    {
        ProcessingMode = SvgProcessingMode.SecureStatic,
        ExternalResources = SvgExternalResourcePolicy.SameDocumentAndDataOnly,
    };

    /// <summary>
    /// Растеризует документ SVG в заданный формат.
    /// </summary>
    /// <param name="svg">Байты документа SVG.</param>
    /// <param name="to">Формат результата: PNG, JPEG или WebP.</param>
    /// <param name="options">Параметры вызова: действия и качество.</param>
    /// <param name="limits">Пределы и умолчания движка.</param>
    /// <param name="cancellationToken">Отмена между этапами.</param>
    /// <returns>Закодированное изображение.</returns>
    /// <exception cref="InvalidDataException">Документ повреждён, пуст или результат больше предела.</exception>
    internal static byte[] Rasterize(byte[] svg, FileFormat to, ConversionOptions options, RasterLimits limits, CancellationToken cancellationToken)
    {
        (SKEncodedImageFormat format, int quality, SKColor background) = Encoding(to, options.Image?.Quality, limits);

        using SKSvg document = new();
        document.Settings.EnableJavaScript = false;
        document.Settings.EnableExternalJavaScript = false;
        document.Settings.EnableBrokenImagePlaceholders = false;

        SKPicture picture = Load(document, svg);
        cancellationToken.ThrowIfCancellationRequested();

        RasterPlan plan = RasterPlan.Build(picture.CullRect, options.Operations, limits.MaxPixels);
        using SKBitmap bitmap = new(new SKImageInfo(plan.Width, plan.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using (SKCanvas canvas = new(bitmap))
        {
            canvas.Clear(background);
            canvas.SetMatrix(plan.Matrix);
            canvas.DrawPicture(picture);
        }

        cancellationToken.ThrowIfCancellationRequested();

        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(format, quality)
            ?? throw new InvalidOperationException($"Кодировщик Skia не смог записать {to.Name}.");
        return data.ToArray();
    }

    // Формат, качество и подложка по формату результата: прозрачность хранят PNG и WebP, JPEG — нет.
    private static (SKEncodedImageFormat Format, int Quality, SKColor Background) Encoding(FileFormat to, int? quality, RasterLimits limits)
    {
        if (to.Equals(FileFormat.Png))
        {
            return (SKEncodedImageFormat.Png, 100, SKColors.Transparent);
        }

        if (to.Equals(FileFormat.Jpeg))
        {
            return (SKEncodedImageFormat.Jpeg, quality ?? limits.DefaultJpegQuality, limits.JpegBackground);
        }

        if (to.Equals(FileFormat.Webp))
        {
            return (SKEncodedImageFormat.Webp, quality ?? limits.DefaultWebpQuality, SKColors.Transparent);
        }

        throw new NotSupportedException($"Движок SVG не умеет формат результата «{to.Name}».");
    }

    // Повреждённый XML и документ без рисунка — одна понятная ошибка с причиной внутри.
    private static SKPicture Load(SKSvg document, byte[] svg)
    {
        try
        {
            using MemoryStream stream = new(svg, writable: false);
            return document.Load(stream, new SvgParameters(null, null, null, SecureLoad))
                ?? throw new InvalidDataException("Документ SVG не содержит рисунка.");
        }
        catch (XmlException ex)
        {
            throw new InvalidDataException($"Документ SVG повреждён: {ex.Message}", ex);
        }
    }
}
