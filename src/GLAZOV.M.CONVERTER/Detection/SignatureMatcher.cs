using System.Buffers.Binary;
using GLAZOV.M.CONVERTER.Data;

namespace GLAZOV.M.CONVERTER.Detection;

/// <summary>
/// Определение типа по начальным байтам: звук (MP3, WAV, OGG, FLAC, M4A) и изображения
/// (PNG, JPEG, WebP, GIF, BMP, TIFF, DDS, PSD, SVG). Расширение имени не учитывается (инвариант проекта INV-001).
/// TGA по содержимому не определяется: в начале файла нет сигнатуры — такой вход получает <see cref="FileFormat.Unknown"/>.
/// </summary>
internal static class SignatureMatcher
{
    /// <summary>
    /// Сколько начальных байтов нужно прочитать: с запасом на самый длинный кадр MP3 (1441 байт)
    /// и заголовок следующего за ним кадра, а также на пролог SVG (объявление, комментарии, DOCTYPE).
    /// </summary>
    internal const int HeadLength = 4096;

    private static readonly int[] Mpeg1Layer3Bitrates = [0, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320];
    private static readonly int[] Mpeg2Layer3Bitrates = [0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160];
    private static readonly int[] Mpeg1SampleRates = [44100, 48000, 32000];
    private static readonly int[] Mpeg2SampleRates = [22050, 24000, 16000];
    private static readonly int[] Mpeg25SampleRates = [11025, 12000, 8000];
    private static readonly uint[] BmpHeaderSizes = [12, 40, 52, 56, 64, 108, 124];

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

        if (head.StartsWith("fLaC"u8))
        {
            return FileFormat.Flac;
        }

        FileFormat? image = MatchImage(head);
        if (image is not null)
        {
            return image;
        }

        if (IsWav(head))
        {
            return FileFormat.Wav;
        }

        if (IsM4a(head))
        {
            return FileFormat.M4a;
        }

        if (IsMp3(head))
        {
            return FileFormat.Mp3;
        }

        return IsSvg(head) ? FileFormat.Svg : FileFormat.Unknown;
    }

    private static FileFormat? MatchImage(ReadOnlySpan<byte> head)
    {
        if (head.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
        {
            return FileFormat.Png;
        }

        if (head.StartsWith(new byte[] { 0xFF, 0xD8, 0xFF }))
        {
            return FileFormat.Jpeg;
        }

        if (head.StartsWith("GIF87a"u8) || head.StartsWith("GIF89a"u8))
        {
            return FileFormat.Gif;
        }

        if (IsTiff(head))
        {
            return FileFormat.Tiff;
        }

        if (IsPsd(head))
        {
            return FileFormat.Psd;
        }

        if (IsDds(head))
        {
            return FileFormat.Dds;
        }

        if (head.Length >= 12 && head.StartsWith("RIFF"u8) && head[8..12].SequenceEqual("WEBP"u8))
        {
            return FileFormat.Webp;
        }

        return IsBmp(head) ? FileFormat.Bmp : null;
    }

    // Страница OGG начинается с «OggS» и нулевой версии формата потока.
    private static bool IsOgg(ReadOnlySpan<byte> head)
    {
        return head.Length >= 5 && head.StartsWith("OggS"u8) && head[4] == 0;
    }

    // RIFF-контейнер с типом WAVE: «RIFF», четыре байта размера, «WAVE».
    private static bool IsWav(ReadOnlySpan<byte> head)
    {
        return head.Length >= 12 && head.StartsWith("RIFF"u8) && head[8..12].SequenceEqual("WAVE"u8);
    }

    // Контейнер MP4: «ftyp» с четвёртого байта и главный бренд звука «M4A »/«M4B ».
    // Бренды isom, mp42 и подобные неоднозначны (видео или звук) и в эту очередь не распознаются.
    private static bool IsM4a(ReadOnlySpan<byte> head)
    {
        return head.Length >= 12
            && head[4..8].SequenceEqual("ftyp"u8)
            && (head[8..12].SequenceEqual("M4A "u8) || head[8..12].SequenceEqual("M4B "u8));
    }

    // «II*\0» (порядок Intel) или «MM\0*» (порядок Motorola).
    private static bool IsTiff(ReadOnlySpan<byte> head)
    {
        return head.StartsWith(new byte[] { 0x49, 0x49, 0x2A, 0x00 }) || head.StartsWith(new byte[] { 0x4D, 0x4D, 0x00, 0x2A });
    }

    // «8BPS», версия 1 (PSD) или 2 (PSB), шесть нулевых байтов.
    private static bool IsPsd(ReadOnlySpan<byte> head)
    {
        return head.Length >= 12
            && head.StartsWith("8BPS"u8)
            && head[4] == 0
            && head[5] is 1 or 2
            && head[6..12].IndexOfAnyExcept((byte)0) < 0;
    }

    // «DDS », размер заголовка 124 и размер описания пикселей 32.
    private static bool IsDds(ReadOnlySpan<byte> head)
    {
        return head.Length >= 80
            && head.StartsWith("DDS "u8)
            && BinaryPrimitives.ReadUInt32LittleEndian(head[4..8]) == 124
            && BinaryPrimitives.ReadUInt32LittleEndian(head[76..80]) == 32;
    }

    // «BM», зарезервированные поля нули, размер заголовка изображения — один из допустимых.
    private static bool IsBmp(ReadOnlySpan<byte> head)
    {
        return head.Length >= 18
            && head.StartsWith("BM"u8)
            && head[6..10].IndexOfAnyExcept((byte)0) < 0
            && BmpHeaderSizes.Contains(BinaryPrimitives.ReadUInt32LittleEndian(head[14..18]));
    }

    // SVG — текст: после метки порядка байтов, объявления XML, комментариев и DOCTYPE первый элемент — svg.
    private static bool IsSvg(ReadOnlySpan<byte> head)
    {
        ReadOnlySpan<byte> rest = head.StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }) ? head[3..] : head;

        while (true)
        {
            rest = TrimStart(rest);
            if (rest.StartsWith("<?"u8))
            {
                rest = SkipPast(rest, "?>"u8);
            }
            else if (rest.StartsWith("<!--"u8))
            {
                rest = SkipPast(rest, "-->"u8);
            }
            else if (rest.StartsWith("<!DOCTYPE"u8) || rest.StartsWith("<!doctype"u8))
            {
                rest = SkipDoctype(rest);
            }
            else
            {
                break;
            }

            if (rest.IsEmpty)
            {
                return false;
            }
        }

        return rest.Length > 4
            && rest.StartsWith("<svg"u8)
            && (rest[4] is (byte)' ' or (byte)'>' or (byte)'/' or (byte)'\t' or (byte)'\r' or (byte)'\n');
    }

    private static ReadOnlySpan<byte> TrimStart(ReadOnlySpan<byte> data)
    {
        int i = 0;
        while (i < data.Length && data[i] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
        {
            i++;
        }

        return data[i..];
    }

    private static ReadOnlySpan<byte> SkipPast(ReadOnlySpan<byte> data, ReadOnlySpan<byte> end)
    {
        int index = data.IndexOf(end);
        return index < 0 ? [] : data[(index + end.Length)..];
    }

    // DOCTYPE может нести внутренний набор в квадратных скобках; конец — «>» вне скобок.
    private static ReadOnlySpan<byte> SkipDoctype(ReadOnlySpan<byte> data)
    {
        int depth = 0;
        for (int i = 0; i < data.Length; i++)
        {
            if (data[i] == (byte)'[')
            {
                depth++;
            }
            else if (data[i] == (byte)']')
            {
                depth--;
            }
            else if (data[i] == (byte)'>' && depth <= 0)
            {
                return data[(i + 1)..];
            }
        }

        return [];
    }

    private static bool IsMp3(ReadOnlySpan<byte> head)
    {
        // Тег ID3v2: «ID3», версия 2–4 (следующий байт версии не 0xFF), байты размера — по 7 бит.
        if (head.Length >= 10 && head.StartsWith("ID3"u8)
            && head[3] is >= 2 and <= 4 && head[4] != 0xFF
            && head[6] < 0x80 && head[7] < 0x80 && head[8] < 0x80 && head[9] < 0x80)
        {
            return true;
        }

        // Без тега — кадр уровня III; чтобы случайные данные не сходили за MP3,
        // требуется второй кадр точно там, где его обещает длина первого.
        if (!TryReadFrame(head, out Mp3Frame first))
        {
            return false;
        }

        int next = first.Length;
        if (head.Length < next + 4 || !TryReadFrame(head[next..], out Mp3Frame second))
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

        int versionBits = (data[1] >> 3) & 0b11;     // 00 — MPEG 2.5, 01 — запрещено, 10 — MPEG 2, 11 — MPEG 1
        int layerBits = (data[1] >> 1) & 0b11;       // 01 — уровень III
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
        frame = new Mp3Frame(versionBits, length);
        return true;
    }

    private readonly record struct Mp3Frame(int Version, int Length);
}
