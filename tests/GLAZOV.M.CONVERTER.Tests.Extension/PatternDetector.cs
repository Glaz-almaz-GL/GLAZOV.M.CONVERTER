using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Extensions;

namespace GLAZOV.M.CONVERTER.Tests.Extension;

/// <summary>
/// Определитель формата по образцу байтов на заданном смещении; окно чтения задаётся отдельно — для проверки «наибольшего из окон».
/// </summary>
public sealed class PatternDetector(byte[] pattern, FileFormat format, int headLength = 8, int offset = 0) : IFormatDetector
{
    public int HeadLength { get; } = headLength;

    public bool ByContent { get; init; }

    public int Calls { get; private set; }

    public FileFormat? Detect(ReadOnlySpan<byte> head)
    {
        Calls++;
        return head.Length >= offset + pattern.Length && head.Slice(offset, pattern.Length).SequenceEqual(pattern) ? format : null;
    }
}
