using GLAZOV.M.CONVERTER.Converting;

namespace GLAZOV.M.CONVERTER.Formats.Options
{
    /// <summary>
    /// Standard transformation operations applicable to visual content (images, video frames, or vector graphics).
    /// </summary>
    public static class VisualOperations
    {
        /// <summary>
        /// Rotates visual content 90 degrees clockwise.
        /// </summary>
        public static IConversionOperation RotateRight { get; } = new RotateOperation(RotationAngle.Degrees90);

        /// <summary>
        /// Rotates visual content 180 degrees clockwise.
        /// </summary>
        public static IConversionOperation Rotate180 { get; } = new RotateOperation(RotationAngle.Degrees180);

        /// <summary>
        /// Rotates visual content 270 degrees clockwise.
        /// </summary>
        public static IConversionOperation RotateLeft { get; } = new RotateOperation(RotationAngle.Degrees270);

        /// <summary>
        /// Scales visual content to the specified percentage of its original dimensions.
        /// </summary>
        /// <param name="percent">Scale percentage greater than zero.</param>
        public static IConversionOperation ScalePercent(int percent)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(percent);
            return new ScalePercentOperation(percent);
        }
    }

    /// <summary>
    /// Clockwise rotation operation by a specified angle in degrees.
    /// </summary>
    public sealed record RotateOperation(RotationAngle Angle) : IConversionOperation
    {
        /// <inheritdoc />
        public string Name => $"Rotate({(int)Angle})";
    }

    /// <summary>
    /// Proportional scaling operation expressed as a percentage of original size.
    /// </summary>
    /// <param name="Percent">Scale percentage greater than zero.</param>
    public sealed record ScalePercentOperation(int Percent) : IConversionOperation
    {
        /// <inheritdoc />
        public string Name => $"ScalePercent({Percent}%)";
    }
}
