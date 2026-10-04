using GLAZOV.M.CONVERTER.Extensions;

namespace GLAZOV.M.CONVERTER.Tests.Extension;

/// <summary>
/// Подставной движок на публичных типах: по умолчанию копирует вход в выход, запоминает последний запрос,
/// может завершиться сбоем, ждать отмены и объявлять поддержку действий.
/// </summary>
public sealed class LambdaEngine(string? toolName, IReadOnlyList<ConversionPair> pairs, bool supportsOperations = false, byte[]? marker = null) : IConversionEngine
{
    public string? ToolName { get; } = toolName;

    public bool SupportsOperations { get; } = supportsOperations;

    public IReadOnlyList<ConversionPair> Pairs { get; } = pairs;

    public int Calls { get; private set; }

    public ConversionRequest? Last { get; private set; }

    public Exception? Crash { get; init; }

    public bool WaitForCancel { get; init; }

    public Action<ConversionRequest>? Inspect { get; init; }

    public async Task ConvertAsync(ConversionRequest request, CancellationToken cancellationToken)
    {
        Calls++;
        Last = request;
        Inspect?.Invoke(request);
        if (Crash is not null)
        {
            throw Crash;
        }

        if (WaitForCancel)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
        }

        if (marker is not null)
        {
            await request.Output.WriteAsync(marker, cancellationToken);
        }

        await request.Input.CopyToAsync(request.Output, cancellationToken);
    }
}
