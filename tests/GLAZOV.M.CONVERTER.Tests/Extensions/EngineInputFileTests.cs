using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Errors;
using GLAZOV.M.CONVERTER.Extensions;
using GLAZOV.M.CONVERTER.IO;
using GLAZOV.M.CONVERTER.Settings;
using GLAZOV.M.CONVERTER.Tests.Extension;

namespace GLAZOV.M.CONVERTER.Tests.Extensions;

/// <summary>
/// An engine from another assembly gets the input as a file through the request, and reports a failed program
/// with the same error the built-in engines raise.
/// </summary>
public sealed class EngineInputFileTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "convtests-" + Guid.NewGuid().ToString("N"));

    private readonly string _tempFolder;

    public EngineInputFileTests()
    {
        _tempFolder = Path.Combine(_folder, "tmp");
        Directory.CreateDirectory(_tempFolder);
    }

    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
    }

    private Converter With(ProbeEngine engine)
    {
        ConverterSettings settings = new() { TempFolder = _tempFolder };
        settings.Extensions.Add(TestExtensions.Create(engine));
        return new Converter(settings);
    }

    private string PutFile(string name)
    {
        string path = Path.Combine(_folder, name);
        File.WriteAllBytes(path, TestFormats.Sample(TestFormats.Alpha));
        return path;
    }

    [Fact]
    public async Task StreamInput_GivesATempCopyWithTheContent_DeletedWhenDisposed()
    {
        ProbeEngine engine = new();

        await With(engine).ConvertAsync(
            Source.Stream(new MemoryStream(TestFormats.Sample(TestFormats.Alpha))),
            Target.Memory(TestFormats.Beta));

        Assert.StartsWith(_tempFolder, engine.Path);
        Assert.True(engine.ExistedWhileOpen);
        Assert.Equal(TestFormats.Sample(TestFormats.Alpha), engine.Content);
        Assert.False(File.Exists(engine.Path));
        Assert.Empty(Directory.GetFiles(_tempFolder));
    }

    [Fact]
    public async Task FileInput_GivesTheFileItself_AndKeepsItAfterDispose()
    {
        ProbeEngine engine = new();
        string input = PutFile("in.alpha");

        await With(engine).ConvertAsync(Source.File(input), Target.Memory(TestFormats.Beta));

        Assert.Equal(input, engine.Path);
        Assert.True(File.Exists(input));
        Assert.Empty(Directory.GetFiles(_tempFolder));
    }

    [Fact]
    public async Task EngineFailureAfterTheFileWasTaken_StillDeletesTheCopy()
    {
        ProbeEngine engine = new() { FailAfterOpen = true };

        ConversionException error = await Assert.ThrowsAsync<ConversionException>(() => With(engine).ConvertAsync(
            Source.Stream(new MemoryStream(TestFormats.Sample(TestFormats.Alpha))),
            Target.Memory(TestFormats.Beta)));

        Assert.Equal(ConversionFailureReason.EngineFailed, error.Reason);
        Assert.False(File.Exists(engine.Path));
        Assert.Empty(Directory.GetFiles(_tempFolder));
    }

    [Fact]
    public async Task CancelledCopy_LeavesNoFile()
    {
        using CancellationTokenSource cancel = new();
        await cancel.CancelAsync();
        ConversionRequest request = new(
            new MemoryStream(TestFormats.Sample(TestFormats.Alpha)), new MemoryStream(), TestFormats.Alpha, TestFormats.Beta, new ConversionOptions(), null, _tempFolder);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request.EnsureInputFileAsync(cancel.Token));

        Assert.Empty(Directory.GetFiles(_tempFolder));
    }

    [Fact]
    public void ToolFailed_IsTheSameErrorAsTheBuiltInOnes()
    {
        string output = string.Join('\n', Enumerable.Range(1, 40).Select(i => "line " + i));

        ConversionException error = ConversionException.ToolFailed("sometool", 3, output);
        ConversionException builtIn = Failures.ToolFailed("sometool", 3, output);

        Assert.Equal(ConversionStage.Conversion, error.Stage);
        Assert.Equal(ConversionFailureReason.ToolFailed, error.Reason);
        Assert.Equal("sometool", error.ToolName);
        Assert.Equal(3, error.ToolExitCode);
        Assert.Equal(builtIn.ToolOutput, error.ToolOutput);
        Assert.Equal(builtIn.Message, error.Message);
        Assert.Contains("line 40", error.ToolOutput);
        Assert.DoesNotContain("line 1\n", error.ToolOutput);
    }

    private sealed class ProbeEngine : IConversionEngine
    {
        public IReadOnlyList<ConversionPair> Pairs { get; } = [new(TestFormats.Alpha, TestFormats.Beta)];

        public bool SupportsOperations => false;

        public IReadOnlyList<ToolSpec> Tools => [];

        public bool FailAfterOpen { get; init; }

        public string? Path { get; private set; }

        public bool ExistedWhileOpen { get; private set; }

        public byte[]? Content { get; private set; }

        public async Task ConvertAsync(ConversionRequest request, CancellationToken cancellationToken)
        {
            await using InputFile file = await request.EnsureInputFileAsync(cancellationToken);
            Path = file.Path;
            ExistedWhileOpen = File.Exists(file.Path);
            Content = await File.ReadAllBytesAsync(file.Path, cancellationToken);
            if (FailAfterOpen)
            {
                throw new InvalidOperationException("the program crashed");
            }
        }
    }
}
