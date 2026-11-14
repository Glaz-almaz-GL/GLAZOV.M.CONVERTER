using System.Text.Unicode;

namespace GLAZOV.M.CONVERTER.Formats.Detection.Text;

/// <summary>
/// Plain text: well-formed UTF-8 (a byte order mark is allowed) or UTF-16 with a byte order mark, with no control characters
/// except tab, line feed, carriage return and form feed. Text has no marker of its own, so this is the last sign to be tried:
/// whatever else the data starts like wins. A sequence cut by the end of the window is not held against the data.
/// </summary>
internal static class TextSignature
{
    private static readonly byte[] Utf8Mark = [0xEF, 0xBB, 0xBF];

    /// <summary>
    /// True when the leading bytes read as text.
    /// </summary>
    /// <param name="head">Leading bytes of the input (the detection window).</param>
    internal static bool Matches(ReadOnlySpan<byte> head)
    {
        if (head.IsEmpty)
        {
            return false;
        }

        if (head.StartsWith(Utf8Mark))
        {
            return IsUtf8Text(head[Utf8Mark.Length..]);
        }

        if (head.Length >= 2 && head[0] == 0xFF && head[1] == 0xFE)
        {
            return IsUtf16Text(head[2..], littleEndian: true);
        }

        return head.Length >= 2 && head[0] == 0xFE && head[1] == 0xFF ? IsUtf16Text(head[2..], littleEndian: false) : IsUtf8Text(head);
    }

    private static bool IsUtf8Text(ReadOnlySpan<byte> bytes)
    {
        ReadOnlySpan<byte> whole = bytes[..(bytes.Length - IncompleteTail(bytes))];
        if (whole.IsEmpty)
        {
            return false;
        }

        foreach (byte b in whole)
        {
            if (IsControl(b))
            {
                return false;
            }
        }

        return Utf8.IsValid(whole);
    }

    // The window may end inside a multi-byte character: up to three bytes of an unfinished sequence are left out of the check.
    private static int IncompleteTail(ReadOnlySpan<byte> bytes)
    {
        for (int back = 1; back <= Math.Min(3, bytes.Length); back++)
        {
            byte b = bytes[^back];
            if ((b & 0xC0) == 0x80)
            {
                continue;
            }

            // Only C2..F4 can start a sequence; C0, C1 and F5..FF are errors, not a cut, and are left to the validity check.
            int expected = b switch { >= 0xF0 and <= 0xF4 => 4, >= 0xE0 and < 0xF0 => 3, >= 0xC2 and < 0xE0 => 2, _ => 1 };
            return expected > back ? back : 0;
        }

        return 0;
    }

    private static bool IsUtf16Text(ReadOnlySpan<byte> bytes, bool littleEndian)
    {
        int units = bytes.Length / 2;
        if (units == 0)
        {
            return false;
        }

        bool expectLowSurrogate = false;

        for (int i = 0; i < units; i++)
        {
            char c = ReadCodeUnit(bytes, i, littleEndian);

            if (expectLowSurrogate)
            {
                if (!char.IsLowSurrogate(c))
                {
                    return false;
                }

                expectLowSurrogate = false;
                continue;
            }

            if (IsDisallowedControl(c) || char.IsLowSurrogate(c))
            {
                return false;
            }

            expectLowSurrogate = char.IsHighSurrogate(c);
        }

        return true;
    }

    private static char ReadCodeUnit(ReadOnlySpan<byte> bytes, int index, bool littleEndian)
    {
        int offset = 2 * index;
        int value = littleEndian
            ? bytes[offset] | (bytes[offset + 1] << 8)
            : (bytes[offset] << 8) | bytes[offset + 1];

        return (char)value;
    }

    private static bool IsDisallowedControl(char c)
    {
        return c < 0x20 ? IsControl((byte)c) : c == 0x7F;
    }


    // Tab, line feed, form feed and carriage return are text; the rest below the space, and DEL, are not.
    private static bool IsControl(byte b)
    {
        return b is < 0x20 and not (0x09 or 0x0A or 0x0C or 0x0D) or 0x7F;
    }
}
