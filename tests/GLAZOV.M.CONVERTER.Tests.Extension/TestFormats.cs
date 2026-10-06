using GLAZOV.M.CONVERTER.Data;

namespace GLAZOV.M.CONVERTER.Tests.Extension;

/// <summary>
/// Stand-in formats for core tests: none of them is known to the library, each has a unique header and MIME type.
/// </summary>
public static class TestFormats
{
    public static readonly FileFormat Alpha = FileFormat.Create("ALPHA", "application/x-test-alpha", "alpha");

    public static readonly FileFormat Beta = FileFormat.Create("BETA", "application/x-test-beta", "beta");

    public static readonly FileFormat Gamma = FileFormat.Create("GAMMA", "application/x-test-gamma", "gamma");

    public static readonly FileFormat Delta = FileFormat.Create("DELTA", "application/x-test-delta", "delta");

    public static readonly FileFormat Epsilon = FileFormat.Create("EPSILON", "application/x-test-epsilon", "epsilon");

    public static readonly FileFormat Zeta = FileFormat.Create("ZETA", "application/x-test-zeta", "zeta");

    /// <summary>
    /// All stand-in formats in declaration order.
    /// </summary>
    public static IReadOnlyList<FileFormat> All { get; } = [Alpha, Beta, Gamma, Delta, Epsilon, Zeta];

    /// <summary>
    /// The header every sample of <paramref name="format"/> starts with: <c>"~TST-"</c> plus the upper-case name and a line break.
    /// </summary>
    public static byte[] Header(FileFormat format)
    {
        return System.Text.Encoding.ASCII.GetBytes("~TST-" + format.Name + "\n");
    }

    /// <summary>
    /// A sample file of <paramref name="format"/>: the header followed by <paramref name="bodyLength"/> filler bytes.
    /// </summary>
    public static byte[] Sample(FileFormat format, int bodyLength = 64)
    {
        byte[] header = Header(format);
        byte[] data = new byte[header.Length + bodyLength];
        header.CopyTo(data, 0);
        for (int i = header.Length; i < data.Length; i++)
        {
            data[i] = (byte)(i * 31 + 7);
        }

        return data;
    }
}
