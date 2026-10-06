using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Extensions;
using GLAZOV.M.CONVERTER.Formats;

namespace GLAZOV.M.CONVERTER.Tests.Extension;

/// <summary>
/// Расширение из эталона v0.5, написанное только на публичных типах библиотеки из другой сборки:
/// свой формат DEMO (признак — начало «DEMO»), движок DEMO → WAV копирует вход в выход.
/// </summary>
public sealed class DemoExtension : IConverterExtension
{
    public static readonly FileFormat Demo = FileFormat.Create("DEMO", "application/x-demo", "demo");

    public string Name => "Демо";

    public IReadOnlyList<IFormatDetector> Detectors { get; } = [new PatternDetector("DEMO"u8.ToArray(), Demo)];

    public IReadOnlyList<IConversionEngine> Engines { get; } = [new LambdaEngine(null, [new ConversionPair(Demo, FileFormat.Wav)])];
}
