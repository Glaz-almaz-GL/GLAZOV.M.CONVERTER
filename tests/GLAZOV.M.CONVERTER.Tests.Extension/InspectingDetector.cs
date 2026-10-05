using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Extensions;

namespace GLAZOV.M.CONVERTER.Tests.Extension;

/// <summary>
/// Определитель второго взгляда: открывает вход, если он начинается с образца, и отвечает тем, что вернёт заданная функция.
/// </summary>
public sealed class InspectingDetector(byte[] pattern, Func<Stream, FileFormat?> inspect) : IStreamFormatDetector
{
    public int HeadLength => pattern.Length;

    public int HeadCalls { get; private set; }

    public int InspectCalls { get; private set; }

    public FileFormat? Detect(ReadOnlySpan<byte> head)
    {
        HeadCalls++;
        return null;
    }

    public bool CanInspect(ReadOnlySpan<byte> head)
    {
        return head.StartsWith(pattern);
    }

    public FileFormat? Detect(Stream input)
    {
        InspectCalls++;
        return inspect(input);
    }
}
