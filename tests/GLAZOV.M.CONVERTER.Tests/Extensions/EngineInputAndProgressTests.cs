using System.Collections.Concurrent;
using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Extensions;
using GLAZOV.M.CONVERTER.Settings;
using GLAZOV.M.CONVERTER.Tests.Extension;

namespace GLAZOV.M.CONVERTER.Tests.Extensions;

/// <summary>
/// An engine from another assembly sees the input path (file input only) and reports progress through the request;
/// the subscriber gets only growing values and the library's own 0 and 100.
/// </summary>
public sealed class EngineInputAndProgressTests : IDisposable
{
    private static readonly FileFormat Demo = DemoExtension.Demo;

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "convtests-" + Guid.NewGuid().ToString("N"));

    public EngineInputAndProgressTests()
    {
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
    }

    private static Converter With(LambdaEngine engine)
    {
        ConverterSettings settings = new();
        settings.Extensions.Add(new ConfigurableExtension("Проба", [new PatternDetector("DEMO"u8.ToArray(), Demo)], [engine]));
        return new Converter(settings);
    }

    private static LambdaEngine Engine(Action<ConversionRequest>? inspect = null)
    {
        return new LambdaEngine(null, [new ConversionPair(Demo, FileFormat.Wav)]) { Inspect = inspect };
    }

    private string Put(string name)
    {
        string path = Path.Combine(_folder, name);
        File.WriteAllBytes(path, "DEMO данные"u8.ToArray());
        return path;
    }

    [Fact]
    public async Task InputPath_IsTheFullPathForAFile_AndNullForAStreamAndMemory()
    {
        List<string?> seen = [];
        List<long> sizes = [];
        Converter converter = With(Engine(request =>
        {
            seen.Add(request.InputPath);
            if (request.InputPath is not null)
            {
                sizes.Add(new FileInfo(request.InputPath).Length);
            }
        }));
        string path = Put("a.dat");
        string relative = Path.GetRelativePath(Environment.CurrentDirectory, path);

        await converter.ConvertAsync(Source.File(path), Target.Memory(FileFormat.Wav));
        await converter.ConvertAsync(Source.File(relative), Target.Memory(FileFormat.Wav));
        await converter.ConvertAsync(Source.Stream(new MemoryStream("DEMO x"u8.ToArray())), Target.Memory(FileFormat.Wav));

        Assert.Equal(new string?[] { path, path, null }, seen);
        Assert.Equal([new FileInfo(path).Length, new FileInfo(path).Length], sizes);
    }

    [Fact]
    public async Task InputPath_IsSetForEveryFileOfABatch()
    {
        ConcurrentBag<string?> seen = [];
        Converter converter = With(Engine(request => seen.Add(request.InputPath)));
        string a = Put("a.dat");
        string b = Put("b.dat");

        await converter.ConvertBatchAsync(Source.Folder(_folder, "*.dat"), Target.Folder(Path.Combine(_folder, "out"), FileFormat.Wav));

        Assert.Equal(new string?[] { a, b }, seen.Order().ToArray());
    }

    [Fact]
    public async Task Progress_OnlyGrowingValues_ReachTheSubscriber_And100IsGivenByTheLibrary()
    {
        Recorder recorder = new();
        Converter converter = With(Engine(request =>
        {
            foreach (int percent in new[] { 10, 40, 40, 20, 100 })
            {
                request.ReportProgress(percent);
            }
        }));

        await converter.ConvertAsync(Source.File(Put("a.dat")), Target.Memory(FileFormat.Wav), progress: recorder);

        Assert.Equal([0, 10, 40, 100], recorder.Values);
    }

    [Fact]
    public async Task Progress_OutOfRangeValuesAreClamped_AndHarmless()
    {
        Recorder recorder = new();
        Converter converter = With(Engine(request =>
        {
            request.ReportProgress(-5);
            request.ReportProgress(150);
            request.ReportProgress(55);
        }));

        await converter.ConvertAsync(Source.File(Put("a.dat")), Target.Memory(FileFormat.Wav), progress: recorder);

        Assert.Equal([0, 55, 100], recorder.Values);
    }

    [Fact]
    public async Task Progress_WithoutASubscriber_DoesNothing()
    {
        Converter converter = With(Engine(request => request.ReportProgress(50)));

        ConversionResult result = await converter.ConvertAsync(Source.File(Put("a.dat")), Target.Memory(FileFormat.Wav));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Progress_FromSeveralThreads_StaysIncreasing()
    {
        Recorder recorder = new();
        Converter converter = With(Engine(request => Parallel.For(1, 100, percent => request.ReportProgress(percent))));

        await converter.ConvertAsync(Source.File(Put("a.dat")), Target.Memory(FileFormat.Wav), progress: recorder);

        Assert.Equal(recorder.Values.Order().ToArray(), recorder.Values);
        Assert.Equal(recorder.Values.Distinct().Count(), recorder.Values.Length);
        Assert.Equal(0, recorder.Values[0]);
        Assert.Equal(100, recorder.Values[^1]);
    }

    private sealed class Recorder : IProgress<ConversionProgress>
    {
        private readonly List<int> _values = [];

        public int[] Values
        {
            get
            {
                lock (_values)
                {
                    return [.. _values];
                }
            }
        }

        public void Report(ConversionProgress value)
        {
            lock (_values)
            {
                _values.Add((int)value.Percent);
            }
        }
    }
}
