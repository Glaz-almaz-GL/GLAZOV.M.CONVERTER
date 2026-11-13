using System.Text;
using GLAZOV.M.CONVERTER.Formats.Detection.Text;

namespace GLAZOV.M.CONVERTER.Formats.Detection.Tables;

/// <summary>
/// Reads the start of a text as rows of fields: a delimiter, a quote or a line break inside quotes does not end a field or a row.
/// The signs of CSV and TSV share it.
/// </summary>
internal static class DelimitedText
{
    private const int MaxRows = 64;

    // Two lines with the same number of commas are common in prose; three are a table far more often.
    private const int MinRows = 3;

    /// <summary>
    /// True when the head reads as text of at least three non-empty rows that all hold the same non-zero number of the delimiter outside quotes.
    /// </summary>
    /// <param name="head">Leading bytes of the input (the detection window).</param>
    /// <param name="delimiters">Delimiters to try in order; the first that fits is the one used.</param>
    /// <param name="fitting">The delimiter that fits, when the result is true.</param>
    internal static bool TryFind(ReadOnlySpan<byte> head, ReadOnlySpan<char> delimiters, out char fitting)
    {
        fitting = '\0';
        if (!TextSignature.Matches(head))
        {
            return false;
        }

        string text = Decode(head);
        if (LooksLikeMarkup(text))
        {
            return false;
        }

        // A window cut mid-row leaves a last row that proves nothing; a whole input without a final line break keeps it.
        bool windowCut = head.Length >= DetectionSizes.MinimumWindow;
        foreach (char delimiter in delimiters)
        {
            if (HasEqualCounts(text, delimiter, windowCut))
            {
                fitting = delimiter;
                return true;
            }
        }

        return false;
    }

    private static string Decode(ReadOnlySpan<byte> head)
    {
        if (head.Length >= 2 && head[0] == 0xFF && head[1] == 0xFE)
        {
            return Encoding.Unicode.GetString(head[2..]);
        }

        if (head.Length >= 2 && head[0] == 0xFE && head[1] == 0xFF)
        {
            return Encoding.BigEndianUnicode.GetString(head[2..]);
        }

        ReadOnlySpan<byte> body = head.Length >= 3 && head[0] == 0xEF && head[1] == 0xBB && head[2] == 0xBF ? head[3..] : head;
        return Encoding.UTF8.GetString(body);
    }

    // JSON, JSON lines and markup are text with commas in it; they are not tables.
    private static bool LooksLikeMarkup(string text)
    {
        foreach (char c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                continue;
            }

            return c is '{' or '[' or '<';
        }

        return true;
    }

    private static bool HasEqualCounts(string text, char delimiter, bool windowCut)
    {
        List<int> counts = [];
        int count = 0;
        bool inQuotes = false;
        bool rowHasContent = false;
        bool rowOpen = false;

        for (int i = 0; i < text.Length && counts.Count < MaxRows; i++)
        {
            char c = text[i];
            rowOpen = true;
            if (c == '"')
            {
                inQuotes = !inQuotes;
                rowHasContent = true;
            }
            else if (inQuotes)
            {
                rowHasContent = true;
            }
            else if (c is '\n' or '\r')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                if (rowHasContent)
                {
                    counts.Add(count);
                }

                count = 0;
                rowHasContent = false;
                rowOpen = false;
            }
            else
            {
                rowHasContent = true;
                if (c == delimiter)
                {
                    count++;
                }
            }
        }

        if (rowOpen && rowHasContent && !inQuotes && !windowCut && counts.Count < MaxRows)
        {
            counts.Add(count);
        }

        return counts.Count >= MinRows && counts[0] > 0 && counts.All(n => n == counts[0]);
    }
}
