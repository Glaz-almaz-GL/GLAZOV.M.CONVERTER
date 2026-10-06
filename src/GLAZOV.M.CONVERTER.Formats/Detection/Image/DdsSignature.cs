using System.Buffers.Binary;

namespace GLAZOV.M.CONVERTER.Formats.Detection.Image;

/// <summary>
/// DDS: "DDS ", header size 124 and pixel format size 32.
/// </summary>
internal static class DdsSignature
{
    /// <summary>
    /// True when the data starts like this format.
    /// </summary>
    /// <param name="head">Leading bytes of the input (the detection window).</param>
    internal static bool Matches(ReadOnlySpan<byte> head)
    {
        return head.Length >= 80
            && head.StartsWith("DDS "u8)
            && BinaryPrimitives.ReadUInt32LittleEndian(head[4..8]) == 124
            && BinaryPrimitives.ReadUInt32LittleEndian(head[76..80]) == 32;
    }
}
