namespace GLAZOV.M.CONVERTER.IO;

/// <summary>
/// The probe of a container tried to read more than the allowed number of bytes.
/// </summary>
internal sealed class ProbeLimitException : IOException
{
    internal ProbeLimitException(long limit)
        : base($"The probe read more than {limit} bytes.")
    {
    }
}
