namespace GLAZOV.M.CONVERTER.Detection.Audio;

/// <summary>
/// MP3: an ID3v2 tag, or a frame of layer III followed exactly where its length promises by a second frame of the same version.
/// </summary>
internal static class Mp3Signature
{
    private static readonly int[] Mpeg1Layer3Bitrates = [0, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320];
    private static readonly int[] Mpeg2Layer3Bitrates = [0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160];
    private static readonly int[] Mpeg1SampleRates = [44100, 48000, 32000];
    private static readonly int[] Mpeg2SampleRates = [22050, 24000, 16000];
    private static readonly int[] Mpeg25SampleRates = [11025, 12000, 8000];

    /// <summary>
    /// True when the data starts like MP3.
    /// </summary>
    /// <param name="head">Leading bytes of the input (the detection window).</param>
    internal static bool Matches(ReadOnlySpan<byte> head)
    {
        // The ID3v2 tag: "ID3", version 2-4 (the next byte of the version is not 0xFF), size bytes of 7 bits each.
        if (head.Length >= 10 && head.StartsWith("ID3"u8)
            && head[3] is >= 2 and <= 4 && head[4] != 0xFF
            && head[6] < 0x80 && head[7] < 0x80 && head[8] < 0x80 && head[9] < 0x80)
        {
            return true;
        }

        // Without a tag it is a frame of layer III; so that random data does not pass for MP3,
        // a second frame is required exactly where the length of the first promises it.
        if (!TryReadFrame(head, out Frame first))
        {
            return false;
        }

        int next = first.Length;
        if (head.Length < next + 4 || !TryReadFrame(head[next..], out Frame second))
        {
            return false;
        }

        return second.Version == first.Version;
    }

    private static bool TryReadFrame(ReadOnlySpan<byte> data, out Frame frame)
    {
        frame = default;
        if (data.Length < 4 || data[0] != 0xFF || (data[1] & 0xE0) != 0xE0)
        {
            return false;
        }

        int versionBits = (data[1] >> 3) & 0b11;     // 00 - MPEG 2.5, 01 - forbidden, 10 - MPEG 2, 11 - MPEG 1
        int layerBits = (data[1] >> 1) & 0b11;       // 01 - layer III
        int bitrateIndex = data[2] >> 4;
        int sampleRateIndex = (data[2] >> 2) & 0b11;
        int padding = (data[2] >> 1) & 1;

        if (versionBits == 0b01 || layerBits != 0b01 || bitrateIndex is 0 or 0xF || sampleRateIndex == 0b11)
        {
            return false;
        }

        bool isMpeg1 = versionBits == 0b11;
        int bitrate = (isMpeg1 ? Mpeg1Layer3Bitrates : Mpeg2Layer3Bitrates)[bitrateIndex] * 1000;
        int sampleRate = (versionBits switch
        {
            0b11 => Mpeg1SampleRates,
            0b10 => Mpeg2SampleRates,
            _ => Mpeg25SampleRates,
        })[sampleRateIndex];

        int length = ((isMpeg1 ? 144 : 72) * bitrate / sampleRate) + padding;
        frame = new Frame(versionBits, length);
        return true;
    }

    private readonly record struct Frame(int Version, int Length);
}
