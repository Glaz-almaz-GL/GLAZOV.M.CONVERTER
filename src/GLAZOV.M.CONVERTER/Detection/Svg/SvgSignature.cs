using System.IO.Compression;
using System.Text;

namespace GLAZOV.M.CONVERTER.Detection.Svg;

/// <summary>
/// Признак SVG: после метки порядка байтов, объявления XML, комментариев и DOCTYPE первый элемент — <c>svg</c>.
/// Понимает текст в UTF-8 и UTF-16 (с меткой порядка байтов и без неё) и сжатый SVGZ (gzip с SVG внутри).
/// </summary>
internal static class SvgSignature
{
    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];
    private static readonly byte[] Utf16LeBom = [0xFF, 0xFE];
    private static readonly byte[] Utf16BeBom = [0xFE, 0xFF];

    // Заголовок gzip: магические байты и метод сжатия deflate.
    private static readonly byte[] GzipMagic = [0x1F, 0x8B, 0x08];

    /// <summary>
    /// Определяет, SVG ли перед нами.
    /// </summary>
    /// <param name="head">Начальные байты входа.</param>
    /// <param name="window">
    /// Окно определения: сколько байтов SVGZ распаковывается для поиска корня; распаковка дальше не идёт —
    /// так сжатый файл не может раздуться в памяти.
    /// </param>
    internal static bool Matches(ReadOnlySpan<byte> head, int window)
    {
        return head.StartsWith(GzipMagic) ? MatchesCompressed(head, window) : MatchesText(head);
    }

    // SVGZ: распаковываем начало и проверяем как обычный текст. Обрезанный поток gzip — не ошибка: окно и есть обрезка.
    private static bool MatchesCompressed(ReadOnlySpan<byte> head, int window)
    {
        byte[] text = new byte[window];
        int total = 0;
        try
        {
            using GZipStream gzip = new(new MemoryStream(head.ToArray(), writable: false), CompressionMode.Decompress);
            int read;
            while (total < text.Length && (read = gzip.Read(text, total, text.Length - total)) > 0)
            {
                total += read;
            }
        }
        catch (InvalidDataException)
        {
            // Повреждённое сжатие: проверяем то, что успело распаковаться.
        }

        return MatchesText(text.AsSpan(0, total));
    }

    // Кодировка — по метке порядка байтов, а без неё — по нулевым байтам вокруг первого знака разметки.
    private static bool MatchesText(ReadOnlySpan<byte> head)
    {
        if (head.StartsWith(Utf8Bom))
        {
            return MatchesUtf8(head[Utf8Bom.Length..]);
        }

        if (head.StartsWith(Utf16LeBom))
        {
            return MatchesUtf16(head[Utf16LeBom.Length..], Encoding.Unicode);
        }

        if (head.StartsWith(Utf16BeBom))
        {
            return MatchesUtf16(head[Utf16BeBom.Length..], Encoding.BigEndianUnicode);
        }

        if (head.Length >= 2 && head[1] == 0 && IsMarkupStart(head[0]))
        {
            return MatchesUtf16(head, Encoding.Unicode);
        }

        if (head.Length >= 2 && head[0] == 0 && IsMarkupStart(head[1]))
        {
            return MatchesUtf16(head, Encoding.BigEndianUnicode);
        }

        return MatchesUtf8(head);
    }

    private static bool IsMarkupStart(byte value)
    {
        return value is (byte)'<' or (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n';
    }

    // Нечётный хвост — половина знака, обрезанная окном: отбрасывается.
    private static bool MatchesUtf16(ReadOnlySpan<byte> head, Encoding encoding)
    {
        string text = encoding.GetString(head[..(head.Length & ~1)]);
        return MatchesUtf8(Encoding.UTF8.GetBytes(text));
    }

    private static bool MatchesUtf8(ReadOnlySpan<byte> head)
    {
        ReadOnlySpan<byte> rest = head;

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
}
