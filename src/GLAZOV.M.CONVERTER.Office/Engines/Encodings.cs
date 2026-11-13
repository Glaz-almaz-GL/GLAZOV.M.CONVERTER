using System.Text;

namespace GLAZOV.M.CONVERTER.Office.Engines;

/// <summary>
/// Finds an encoding by name, with the code pages (windows-1251 and the like) available on every system.
/// Bad bytes and characters the encoding cannot hold throw instead of being replaced.
/// </summary>
internal static class Encodings
{
    static Encodings()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    /// <summary>
    /// The strict encoding with the given name.
    /// </summary>
    /// <param name="name">Encoding name.</param>
    /// <param name="setting">Name of the setting, for the message.</param>
    internal static Encoding Find(string name, string setting)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException($"{setting} is empty.", setting);
        }

        try
        {
            return Encoding.GetEncoding(name, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
        }
        catch (ArgumentException ex)
        {
            throw new ArgumentException($"{setting}: encoding «{name}» is not known on this machine.", setting, ex);
        }
    }

    /// <summary>
    /// True for UTF-8: the encoding LibreOffice writes itself, so no conversion is needed.
    /// </summary>
    /// <param name="encoding">The encoding to test.</param>
    internal static bool IsUtf8(Encoding encoding)
    {
        return encoding.CodePage == 65001;
    }
}
