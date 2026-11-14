using GLAZOV.M.CONVERTER.Formats.Detection.Text;
using System.Text;
using System.Text.Unicode;

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
        if (head.IsEmpty || (!TextSignature.Matches(head) && !IsSingleByteText(head)))
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

    private static bool IsSingleByteText(ReadOnlySpan<byte> bytes)
    {
        foreach (byte b in bytes)
        {
            if (b is (< 0x20 and not (0x09 or 0x0A or 0x0C or 0x0D)) or 0x7F)
            {
                return false;
            }
        }

        return true;
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
        return Utf8.IsValid(body)
            ? Encoding.UTF8.GetString(body)
            : Encoding.Latin1.GetString(body);
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
        RowCountState state = new(delimiter);

        for (int i = 0; i < text.Length && counts.Count < MaxRows; i++)
        {
            state.ProcessChar(text[i], counts);
        }

        state.CompleteIfNeeded(counts, windowCut);

        return counts.Count >= MinRows && counts[0] > 0 && counts.All(n => n == counts[0]);
    }

    private ref struct RowCountState(char delimiter)
    {
        private int _count = 0;
        private bool _inQuotes = false;
        private bool _rowHasContent = false;
        private bool _rowOpen = false;
        private bool _lastWasCarriageReturn = false;

        public void ProcessChar(char c, List<int> counts)
        {
            if (ShouldSkipLineFeed(c))
            {
                return;
            }

            _rowOpen = true;

            if (c == '"')
            {
                _inQuotes = !_inQuotes;
                _rowHasContent = true;
                return;
            }

            if (_inQuotes)
            {
                _rowHasContent = true;
                return;
            }

            if (c is '\n' or '\r')
            {
                CompleteRow(counts, c == '\r');
                return;
            }

            _rowHasContent = true;
            if (c == delimiter)
            {
                _count++;
            }
        }

        private bool ShouldSkipLineFeed(char c)
        {
            if (!_lastWasCarriageReturn)
            {
                return false;
            }

            _lastWasCarriageReturn = false;
            return c == '\n';
        }

        private void CompleteRow(List<int> counts, bool isCarriageReturn)
        {
            if (_rowHasContent)
            {
                counts.Add(_count);
            }

            _count = 0;
            _rowHasContent = false;
            _rowOpen = false;
            _lastWasCarriageReturn = isCarriageReturn;
        }

        public readonly void CompleteIfNeeded(List<int> counts, bool windowCut)
        {
            if (_rowOpen && _rowHasContent && !_inQuotes && !windowCut && counts.Count < MaxRows)
            {
                counts.Add(_count);
            }
        }
    }
}