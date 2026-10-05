using GLAZOV.M.CONVERTER.Pdf.Rendering;
using SkiaSharp;

namespace GLAZOV.M.CONVERTER.Pdf.Tests.Rendering;

/// <summary>
/// The eight EXIF orientations: where the stored top left and top right corners of a picture end up.
/// </summary>
public sealed class OrientationMatrixTests
{
    private const int W = 40;
    private const int H = 20;

    public static TheoryData<SKEncodedOrigin, float, float, float, float> Cases => new()
    {
        { SKEncodedOrigin.TopLeft, 0, 0, W, 0 },
        { SKEncodedOrigin.TopRight, W, 0, 0, 0 },
        { SKEncodedOrigin.BottomRight, W, H, 0, H },
        { SKEncodedOrigin.BottomLeft, 0, H, W, H },
        { SKEncodedOrigin.LeftTop, 0, 0, 0, W },
        { SKEncodedOrigin.RightTop, H, 0, H, W },
        { SKEncodedOrigin.RightBottom, H, W, H, 0 },
        { SKEncodedOrigin.LeftBottom, 0, W, 0, 0 },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void StoredCorners_LandWhereTheOrientationSays(SKEncodedOrigin origin, float x0, float y0, float x1, float y1)
    {
        SKMatrix matrix = OrientationMatrix.For(origin, W, H);

        SKPoint topLeft = matrix.MapPoint(0, 0);
        SKPoint topRight = matrix.MapPoint(W, 0);

        Assert.Equal((x0, y0), (topLeft.X, topLeft.Y));
        Assert.Equal((x1, y1), (topRight.X, topRight.Y));
    }

    [Theory]
    [InlineData(SKEncodedOrigin.TopLeft, false)]
    [InlineData(SKEncodedOrigin.TopRight, false)]
    [InlineData(SKEncodedOrigin.BottomRight, false)]
    [InlineData(SKEncodedOrigin.BottomLeft, false)]
    [InlineData(SKEncodedOrigin.LeftTop, true)]
    [InlineData(SKEncodedOrigin.RightTop, true)]
    [InlineData(SKEncodedOrigin.RightBottom, true)]
    [InlineData(SKEncodedOrigin.LeftBottom, true)]
    public void SidesAreSwapped_OnlyForQuarterTurns(SKEncodedOrigin origin, bool swaps)
    {
        Assert.Equal(swaps, OrientationMatrix.SwapsSides(origin));
    }
}
