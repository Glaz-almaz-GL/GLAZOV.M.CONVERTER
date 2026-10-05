using SkiaSharp;

namespace GLAZOV.M.CONVERTER.Pdf.Rendering;

/// <summary>
/// The turn that a picture's EXIF orientation asks for.
/// </summary>
internal static class OrientationMatrix
{
    /// <summary>
    /// True when the orientation swaps width and height (a quarter turn or a transpose).
    /// </summary>
    /// <param name="origin">Orientation stored in the file.</param>
    internal static bool SwapsSides(SKEncodedOrigin origin)
    {
        return origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
    }

    /// <summary>
    /// Matrix that moves a picture of the stored size into its upright place.
    /// </summary>
    /// <param name="origin">Orientation stored in the file.</param>
    /// <param name="width">Stored width in pixels.</param>
    /// <param name="height">Stored height in pixels.</param>
    internal static SKMatrix For(SKEncodedOrigin origin, int width, int height)
    {
        return origin switch
        {
            SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, width, 0, 1, 0, 0, 0, 1),
            SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, width, 0, -1, height, 0, 0, 1),
            SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, height, 0, 0, 1),
            SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightTop => new SKMatrix(0, -1, height, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, height, -1, 0, width, 0, 0, 1),
            SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, width, 0, 0, 1),
            _ => SKMatrix.Identity,
        };
    }
}
