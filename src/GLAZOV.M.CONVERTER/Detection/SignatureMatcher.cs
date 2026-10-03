using GLAZOV.M.CONVERTER.Data;

namespace GLAZOV.M.CONVERTER.Detection;

/// <summary>
/// Определение типа по начальным байтам: MP3, WAV, OGG. Расширение имени не учитывается (инвариант проекта INV-001).
/// </summary>
internal static class SignatureMatcher
{
    /// <summary>
    /// Сколько начальных байтов нужно прочитать: с запасом на самый длинный кадр MP3 (1441 байт)
    /// и заголовок следующего за ним кадра.
    /// </summary>
    internal const int HeadLength = 4096;

    private static readonly int[] Mpeg1Layer3Bitrates = [0, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320];
    private static readonly int[] Mpeg2Layer3Bitrates = [0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160];
    private static readonly int[] Mpeg1SampleRates = [44100, 48000, 32000];
    private static readonly int[] Mpeg2SampleRates = [22050, 24000, 16000];
    private static readonly int[] Mpeg25SampleRates = [11025, 12000, 8000];

    /// <summary>
    /// Определяет тип по началу данных.
    /// </summary>
    /// <param name="head">Начальные байты входа; пустой и короткий буфер — не ошибка, а <see cref="FileFormat.Unknown"/>.</param>
    internal static FileFormat Match(ReadOnlySpan<byte> head)
    {
        if (IsOgg(head))
        {
            return FileFormat.Ogg;
        }

        if (IsWav(head))
        {
            return FileFormat.Wav;
        }

        if (IsMp3(head))
        {
            return FileFormat.Mp3;
        }

        return FileFormat.Unknown;
    }

    // Страница OGG начинается с «OggS» и нулевой версии формата потока.
    private static bool IsOgg(ReadOnlySpan<byte> head) =>
        head.Length >= 5 && head[0] == 'O' && head[1] == 'g' && head[2] == 'g' && head[3] == 'S' && head[4] == 0;

    // RIFF-контейнер с типом WAVE: «RIFF», четыре байта размера, «WAVE».
    private static bool IsWav(ReadOnlySpan<byte> head) =>
        head.Length >= 12
        && head[0] == 'R' && head[1] == 'I' && head[2] == 'F' && head[3] == 'F'
        && head[8] == 'W' && head[9] == 'A' && head[10] == 'V' && head[11] == 'E';

    private static bool IsMp3(ReadOnlySpan<byte> head)
    {
        // Тег ID3v2: «ID3», версия 2–4 (следующий байт версии не 0xFF), байты размера — по 7 бит.
        if (head.Length >= 10 && head[0] == 'I' && head[1] == 'D' && head[2] == '3'
            && head[3] is >= 2 and <= 4 && head[4] != 0xFF
            && head[6] < 0x80 && head[7] < 0x80 && head[8] < 0x80 && head[9] < 0x80)
        {
            return true;
        }

        // Без тега — кадр уровня III; чтобы случайные данные не сходили за MP3,
        // требуется второй кадр точно там, где его обещает длина первого.
        if (!TryReadFrame(head, out var first))
        {
            return false;
        }

        var next = first.Length;
        if (head.Length < next + 4 || !TryReadFrame(head[next..], out var second))
        {
            return false;
        }

        return second.Version == first.Version;
    }

    private static bool TryReadFrame(ReadOnlySpan<byte> data, out Mp3Frame frame)
    {
        frame = default;
        if (data.Length < 4 || data[0] != 0xFF || (data[1] & 0xE0) != 0xE0)
        {
            return false;
        }

        var versionBits = (data[1] >> 3) & 0b11;     // 00 — MPEG 2.5, 01 — запрещено, 10 — MPEG 2, 11 — MPEG 1
        var layerBits = (data[1] >> 1) & 0b11;       // 01 — уровень III
        var bitrateIndex = data[2] >> 4;
        var sampleRateIndex = (data[2] >> 2) & 0b11;
        var padding = (data[2] >> 1) & 1;

        if (versionBits == 0b01 || layerBits != 0b01 || bitrateIndex is 0 or 0xF || sampleRateIndex == 0b11)
        {
            return false;
        }

        var isMpeg1 = versionBits == 0b11;
        var bitrate = (isMpeg1 ? Mpeg1Layer3Bitrates : Mpeg2Layer3Bitrates)[bitrateIndex] * 1000;
        var sampleRate = (versionBits switch
        {
            0b11 => Mpeg1SampleRates,
            0b10 => Mpeg2SampleRates,
            _ => Mpeg25SampleRates,
        })[sampleRateIndex];

        var length = ((isMpeg1 ? 144 : 72) * bitrate / sampleRate) + padding;
        frame = new Mp3Frame(versionBits, length);
        return true;
    }

    private readonly record struct Mp3Frame(int Version, int Length);
}
