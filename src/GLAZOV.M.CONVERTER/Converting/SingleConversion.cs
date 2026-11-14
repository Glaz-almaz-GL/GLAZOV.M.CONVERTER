using GLAZOV.M.CONVERTER.Capabilities;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Detection;
using GLAZOV.M.CONVERTER.Engines;
using GLAZOV.M.CONVERTER.Errors;
using GLAZOV.M.CONVERTER.Extensions;
using GLAZOV.M.CONVERTER.IO;
using GLAZOV.M.CONVERTER.Settings;
using GLAZOV.M.CONVERTER.Tooling;
using System.Diagnostics;

namespace GLAZOV.M.CONVERTER.Converting;

/// <summary>
/// Executes a single conversion pipeline: opens input, detects format by leading bytes, selects an engine,
/// verifies required external tools, runs the conversion, and commits the output.
/// </summary>
internal sealed class SingleConversion
{
    private readonly ConverterSettingsSnapshot _settings;
    private readonly EngineRegistry _registry;
    private readonly ToolLocator _locator;
    private readonly DetectorChain _chain;

    internal SingleConversion(ConverterSettingsSnapshot settings, EngineRegistry registry, ToolLocator locator, DetectorChain chain)
    {
        _settings = settings;
        _registry = registry;
        _locator = locator;
        _chain = chain;
    }

    /// <summary>
    /// Converts a single file or stream source into the specified target.
    /// </summary>
    internal async Task<ConversionResult> RunAsync(
        Source source,
        Target target,
        ConversionOptions? options,
        IProgress<ConversionProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (source.Kind == SourceKind.Folder)
        {
            throw new ArgumentException("A folder source cannot be converted as a single input; use batch conversion instead.", nameof(source));
        }

        Stopwatch watch = Stopwatch.StartNew();
        await using InputHandle input = await InputReader.OpenAsync(source, cancellationToken).ConfigureAwait(false);

        FileFormat from = await ConfirmTypeAsync(input, cancellationToken).ConfigureAwait(false);
        ConversionOptions callOptions = options ?? new ConversionOptions();
        IConversionEngine engine = ChooseEngine(from, target.FileFormat, callOptions);
        (ToolStatus? tool, IReadOnlyDictionary<string, ToolStatus> tools) = LocateTools(engine);

        bool multiFile = target.Kind == TargetKind.Folder && WritesSeveralFiles(engine, from, target.FileFormat);
        await using OutputSink sink = await OutputSink.OpenAsync(target, input.Name, _settings, multiFile, cancellationToken: cancellationToken).ConfigureAwait(false);

        progress?.Report(new ConversionProgress(0));
        ProgressGate? gate = progress is null ? null : new ProgressGate(progress);

        ConversionRequest request = new(input.Stream, sink.Stream, from, target.FileFormat, callOptions, tool, _settings.TempFolder ?? Path.GetTempPath())
        {
            InputPath = input.Path,
            OutputFolder = sink.StagingFolder,
            OnProgress = gate is null ? null : gate.Report,
            Tools = tools,
        };

        await RunEngineAsync(engine, request, cancellationToken).ConfigureAwait(false);
        watch.Stop();

        ConversionResult result = await sink.CompleteAsync(from, watch.Elapsed, request.Reencoded, request.Notes, engine.GetType().Name, cancellationToken).ConfigureAwait(false);
        progress?.Report(new ConversionProgress(100));
        return result;
    }

    private static bool WritesSeveralFiles(IConversionEngine engine, FileFormat from, FileFormat to)
    {
        return engine.Pairs.Any(pair => pair.From.Equals(from) && pair.To.Equals(to) && pair.WritesSeveralFiles);
    }

    private async Task<FileFormat> ConfirmTypeAsync(InputHandle input, CancellationToken cancellationToken)
    {
        FileFormat from = await InputDetection.DetectAsync(input, _chain, cancellationToken).ConfigureAwait(false);
        if (from.Equals(FileFormat.Unknown))
        {
            string extension = Path.GetExtension(input.Name).TrimStart('.').ToUpperInvariant();
            throw Failures.TypeNotConfirmed(input.Name, extension.Length == 0 ? null : extension, from);
        }

        return from;
    }

    private IConversionEngine ChooseEngine(FileFormat from, FileFormat to, ConversionOptions options)
    {
        IConversionEngine engine = _registry.Find(from, to) ?? throw Failures.UnsupportedConversion(from, to);
        if (options.Operations.Any() && !engine.SupportsOperations)
        {
            // The engine does not support the operations, but the options include them.
            throw Failures.UnsupportedOperations(from, to);
        }

        return engine;
    }


    private (ToolStatus? Tool, IReadOnlyDictionary<string, ToolStatus> Tools) LocateTools(IConversionEngine engine)
    {
        Dictionary<string, ToolStatus> tools = new(StringComparer.OrdinalIgnoreCase);
        ToolStatus? first = null;
        foreach (ToolSpec spec in engine.Tools)
        {
            ToolStatus status = _locator.Locate(spec, engine.Tools);
            if (spec.Required)
            {
                if (!status.Found)
                {
                    throw Failures.ToolNotFound(spec.Name, status.InstallHint);
                }

                first ??= status;
            }

            tools[spec.Name] = status;
        }

        return (first, tools);
    }

    private static async Task RunEngineAsync(IConversionEngine engine, ConversionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await engine.ConvertAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not (ConversionException or OperationCanceledException))
        {
            throw Failures.EngineFailed(engine.GetType().Name, ex);
        }
    }

    /// <summary>
    /// Clamps engine progress values, filters out non-increasing values, and reserves 100% until output commit completes.
    /// </summary>
    private sealed class ProgressGate(IProgress<ConversionProgress> target)
    {
        private readonly Lock _lock = new();
        private int _last;

        internal void Report(int percent)
        {
            int value = Math.Clamp(percent, 0, 100);
            lock (_lock)
            {
                if (value <= _last || value >= 100)
                {
                    return;
                }

                _last = value;
                target.Report(new ConversionProgress(value));
            }
        }
    }
}