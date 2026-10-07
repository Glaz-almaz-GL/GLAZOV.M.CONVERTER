using System.Buffers.Binary;

namespace GLAZOV.M.CONVERTER.Formats.Detection.Image;

/// <summary>
/// ICO: "00 00 01 00", at least one picture, every directory entry visible in the window has a reserved byte of zero,
/// a non-empty body that starts after the directory. CUR files ("00 00 02 00") are not ICO.
/// </summary>
internal static class IcoSignature
{
    private const int HeaderSize = 6;
    private const int EntrySize = 16;

    /// <summary>
    /// True when the data starts like this format.
    /// </summary>
    /// <param name="head">Leading bytes of the input (the detection window).</param>
    internal static bool Matches(ReadOnlySpan<byte> head)
    {
        if (head.Length < HeaderSize + EntrySize || !head[..4].SequenceEqual(new byte[] { 0, 0, 1, 0 }))
        {
            return false;
        }

        int count = BinaryPrimitives.ReadUInt16LittleEndian(head[4..6]);
        if (count == 0)
        {
            return false;
        }

        uint directoryEnd = (uint)(HeaderSize + (count * EntrySize));
        for (int index = 0; index < count && HeaderSize + ((index + 1) * EntrySize) <= head.Length; index++)
        {
            ReadOnlySpan<byte> entry = head.Slice(HeaderSize + (index * EntrySize), EntrySize);
            ushort planes = BinaryPrimitives.ReadUInt16LittleEndian(entry[4..6]);
            uint length = BinaryPrimitives.ReadUInt32LittleEndian(entry[8..12]);
            uint offset = BinaryPrimitives.ReadUInt32LittleEndian(entry[12..16]);
            if (entry[3] != 0 || planes > 1 || length == 0 || offset < directoryEnd || offset > int.MaxValue)
            {
                return false;
            }
        }

        return true;
    }
}
