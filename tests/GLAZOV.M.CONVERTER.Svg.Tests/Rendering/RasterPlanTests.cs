using GLAZOV.M.CONVERTER.Formats.Options;
using GLAZOV.M.CONVERTER.Svg.Rendering;
using SkiaSharp;

namespace GLAZOV.M.CONVERTER.Svg.Tests.Rendering;

/// <summary>
/// План растеризации: естественный размер, поворот и масштаб по порядку, пределы.
/// </summary>
public class RasterPlanTests
{
    private const long NoLimit = long.MaxValue;

    [Fact]
    public void NaturalSize_IsRoundedUp_AndOriginMovedToZero()
    {
        RasterPlan plan = RasterPlan.Build(new SKRect(5, 7, 5 + 100.2f, 7 + 50), [], NoLimit);

        Assert.Equal((101, 50), (plan.Width, plan.Height));
        Assert.Equal(new SKPoint(0, 0), plan.Matrix.MapPoint(5, 7));
    }

    [Fact]
    public void RotateRight_SwapsSides_AndMovesTopLeftToTopRight()
    {
        RasterPlan plan = RasterPlan.Build(SKRect.Create(120, 80), [VisualOperations.RotateRight], NoLimit);

        Assert.Equal((80, 120), (plan.Width, plan.Height));
        Assert.Equal(new SKPoint(80, 0), plan.Matrix.MapPoint(0, 0));
        Assert.Equal(new SKPoint(0, 120), plan.Matrix.MapPoint(120, 80));
    }

    [Fact]
    public void ScalePercent_RoundsDown_AndNeverBelowOnePixel()
    {
        RasterPlan half = RasterPlan.Build(SKRect.Create(101, 51), [VisualOperations.ScalePercent(50)], NoLimit);
        RasterPlan tiny = RasterPlan.Build(SKRect.Create(10, 10), [VisualOperations.ScalePercent(1)], NoLimit);

        Assert.Equal((50, 25), (half.Width, half.Height));
        Assert.Equal((1, 1), (tiny.Width, tiny.Height));
    }

    [Fact]
    public void Operations_AreAppliedInOrder()
    {
        RasterPlan plan = RasterPlan.Build(
            SKRect.Create(200, 100),
            [VisualOperations.ScalePercent(50), VisualOperations.RotateRight, VisualOperations.ScalePercent(200)],
            NoLimit);

        Assert.Equal((100, 200), (plan.Width, plan.Height));
        Assert.Equal(new SKPoint(100, 0), plan.Matrix.MapPoint(0, 0));
        Assert.Equal(new SKPoint(0, 200), plan.Matrix.MapPoint(200, 100));
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(10, 0)]
    public void DocumentWithoutSize_IsRejected(float width, float height)
    {
        Assert.Throws<InvalidDataException>(() => RasterPlan.Build(SKRect.Create(width, height), [], NoLimit));
    }

    [Fact]
    public void ResultAboveMaxPixels_IsRejected_WithTheSettingName()
    {
        InvalidDataException ex = Assert.Throws<InvalidDataException>(() =>
            RasterPlan.Build(SKRect.Create(100, 100), [VisualOperations.ScalePercent(200)], maxPixels: 30_000));

        Assert.Contains("MaxPixels", ex.Message);
    }

    [Fact]
    public void ResultExactlyAtMaxPixels_IsAccepted()
    {
        RasterPlan plan = RasterPlan.Build(SKRect.Create(100, 100), [], maxPixels: 10_000);

        Assert.Equal((100, 100), (plan.Width, plan.Height));
    }

    [Fact]
    public void EnormousScale_IsRejected_BeforeOverflow()
    {
        Assert.Throws<InvalidDataException>(() =>
            RasterPlan.Build(SKRect.Create(1_000_000, 1), [VisualOperations.ScalePercent(int.MaxValue)], NoLimit));
    }
}
