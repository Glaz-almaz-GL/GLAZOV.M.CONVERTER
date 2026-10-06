using System.Text;

namespace GLAZOV.M.CONVERTER.Formats.Detection.Documents;

/// <summary>
/// HTML: after spaces and a UTF-8 byte order mark the data starts with a markup of a page.
/// </summary>
internal static class HtmlSignature
{
    internal static bool Matches(ReadOnlySpan<byte> head)
    {
        ReadOnlySpan<byte> rest = head.StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }) ? head[3..] : head;
        int start = 0;
        while (start < rest.Length && rest[start] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
        {
            start++;
        }

        ReadOnlySpan<byte> text = rest[start..];
        foreach (string prefix in new[] { "<!doctype html", "<html", "<head", "<body" })
        {
            if (text.Length >= prefix.Length
                && Encoding.ASCII.GetString(text[..prefix.Length]).Equals(prefix, StringComparison.OrdinalIgnoreCase)
                && (text.Length == prefix.Length || text[prefix.Length] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n' or (byte)'>' or (byte)'/'))
            {
                return true;
            }
        }

        return false;
    }
}
