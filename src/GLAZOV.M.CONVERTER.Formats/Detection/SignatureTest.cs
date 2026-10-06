namespace GLAZOV.M.CONVERTER.Formats.Detection;

/// <summary>
/// A test of the leading bytes of an input against the sign of one format.
/// </summary>
/// <param name="head">Leading bytes of the input (the detection window).</param>
internal delegate bool SignatureTest(ReadOnlySpan<byte> head);
