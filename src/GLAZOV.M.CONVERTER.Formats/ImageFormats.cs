using GLAZOV.M.CONVERTER.Data;

namespace GLAZOV.M.CONVERTER.Formats;

/// <summary>
/// The image formats of the standard set.
/// </summary>
public static class ImageFormats
{
    /// <summary>
    /// JPEG.
    /// </summary>
    public static FileFormat Jpeg { get; } = FileFormat.Create("JPEG", "image/jpeg", "jpg");

    /// <summary>
    /// PNG, lossless image.
    /// </summary>
    public static FileFormat Png { get; } = FileFormat.Create("PNG", "image/png", "png");

    /// <summary>
    /// WebP.
    /// </summary>
    public static FileFormat Webp { get; } = FileFormat.Create("WebP", "image/webp", "webp");

    /// <summary>
    /// BMP.
    /// </summary>
    public static FileFormat Bmp { get; } = FileFormat.Create("BMP", "image/bmp", "bmp");

    /// <summary>
    /// GIF.
    /// </summary>
    public static FileFormat Gif { get; } = FileFormat.Create("GIF", "image/gif", "gif");

    /// <summary>
    /// TIFF.
    /// </summary>
    public static FileFormat Tiff { get; } = FileFormat.Create("TIFF", "image/tiff", "tiff");

    /// <summary>
    /// TGA. Not detected by content: the start of the file has no sign.
    /// </summary>
    public static FileFormat Tga { get; } = FileFormat.Create("TGA", "image/x-tga", "tga");

    /// <summary>
    /// DDS.
    /// </summary>
    public static FileFormat Dds { get; } = FileFormat.Create("DDS", "image/vnd.ms-dds", "dds");

    /// <summary>
    /// PSD.
    /// </summary>
    public static FileFormat Psd { get; } = FileFormat.Create("PSD", "image/vnd.adobe.photoshop", "psd");

    /// <summary>
    /// SVG, vector image.
    /// </summary>
    public static FileFormat Svg { get; } = FileFormat.Create("SVG", "image/svg+xml", "svg");
}
