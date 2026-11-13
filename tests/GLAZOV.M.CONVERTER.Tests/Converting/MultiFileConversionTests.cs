using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Engines;
using GLAZOV.M.CONVERTER.Errors;
using GLAZOV.M.CONVERTER.Extensions;
using GLAZOV.M.CONVERTER.Settings;
using GLAZOV.M.CONVERTER.Tests.Extension;

namespace GLAZOV.M.CONVERTER.Tests.Converting;

/// <summary>
/// A pair that writes several files: a folder target gets all of them under the overwrite policy or none; other targets get one file
/// and a note; the report lists every file; failure, cancellation and a refused set leave nothing behind; old pairs are unchanged.
/// </summary>
public sealed class MultiFileConversionTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "convtests-" + Guid.NewGuid().ToString("N"));

    public MultiFileConversionTests()
    {
        Directory.CreateDirectory(Path.Combine(_folder, "in"));
        Directory.CreateDirectory(Path.Combine(_folder, "tmp"));
    }

    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
    }

    private string Out => Path.Combine(_folder, "out");

    private string Input(string name)
    {
        string path = Path.Combine(_folder, "in", name);
        File.WriteAllBytes(path, TestFormats.Sample(TestFormats.Alpha));
        return path;
    }

    private Converter Make(IConversionEngine engine, bool overwrite = false)
    {
        ConverterSettings settings = new() { TempFolder = Path.Combine(_folder, "tmp"), OverwriteExisting = overwrite };
        settings.Extensions.Add(TestExtensions.Create());
        return new Converter(settings, new EngineRegistry([engine]), null);
    }

    private string[] OutFiles()
    {
        return Directory.Exists(Out) ? [.. Directory.GetFileSystemEntries(Out, "*", SearchOption.AllDirectories).Select(p => Path.GetRelativePath(Out, p)).Order()] : [];
    }

    [Fact]
    public async Task FolderTarget_GetsEveryFile_AndTheResultListsThem()
    {
        MultiFileEngine engine = new(TestFormats.Alpha, TestFormats.Beta, files: 3);

        ConversionResult result = await Make(engine).ConvertAsync(Source.File(Input("book.alpha")), Target.Folder(Out, TestFormats.Beta));

        string[] expected = ["book-sheet1.beta", "book-sheet2.beta", "book-sheet3.beta"];
        Assert.Equal(expected, OutFiles());
        Assert.Equal(expected, result.OutputPaths.Select(Path.GetFileName));
        Assert.All(result.OutputPaths, path => Assert.Equal(Path.GetFullPath(Out), Path.GetDirectoryName(path)));
        Assert.Equal(result.OutputPaths[0], result.OutputPath);
        Assert.Empty(result.Notes);
        Assert.NotEqual(Path.GetFullPath(Out), Path.GetFullPath(engine.FolderSeen!));
    }

    [Fact]
    public async Task FileTarget_GetsOneFile_AndTheNoteReachesTheResult()
    {
        MultiFileEngine engine = new(TestFormats.Alpha, TestFormats.Beta);
        string output = Path.Combine(Out, "one.beta");

        ConversionResult result = await Make(engine).ConvertAsync(Source.File(Input("book.alpha")), Target.File(output, TestFormats.Beta));

        Assert.Null(engine.FolderSeen);
        Assert.Equal([output], result.OutputPaths);
        Assert.Equal(["book of 3 sheets, sheet 1 taken"], result.Notes);
        Assert.Equal(["one.beta"], OutFiles());
    }

    [Fact]
    public async Task MemoryTarget_GetsOneFileInMemory_WithTheNote()
    {
        ConversionResult result = await Make(new MultiFileEngine(TestFormats.Alpha, TestFormats.Beta)).ConvertAsync(Source.File(Input("book.alpha")), Target.Memory(TestFormats.Beta));

        Assert.NotNull(result.Bytes);
        Assert.Empty(result.OutputPaths);
        Assert.Single(result.Notes);
    }

    [Fact]
    public async Task EngineThrowsAfterTheFirstFile_NothingRemains()
    {
        MultiFileEngine engine = new(TestFormats.Alpha, TestFormats.Beta) { FailAfter = 1 };

        ConversionException error = await Assert.ThrowsAsync<ConversionException>(
            () => Make(engine).ConvertAsync(Source.File(Input("book.alpha")), Target.Folder(Out, TestFormats.Beta)));

        Assert.Equal(ConversionFailureReason.EngineFailed, error.Reason);
        Assert.Empty(OutFiles());
    }

    [Fact]
    public async Task Cancellation_AfterTheFirstFile_LeavesNothing()
    {
        MultiFileEngine engine = new(TestFormats.Alpha, TestFormats.Beta) { WaitForCancelAfterFirst = true };
        using CancellationTokenSource cts = new(TimeSpan.FromMilliseconds(300));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Make(engine).ConvertAsync(Source.File(Input("book.alpha")), Target.Folder(Out, TestFormats.Beta), cancellationToken: cts.Token));

        Assert.Empty(OutFiles());
    }

    [Fact]
    public async Task ExistingFile_IsNotOverwrittenSilently_AndNoFileOfTheSetIsWritten()
    {
        Directory.CreateDirectory(Out);
        string taken = Path.Combine(Out, "book-sheet2.beta");
        File.WriteAllText(taken, "mine");

        ConversionException error = await Assert.ThrowsAsync<ConversionException>(
            () => Make(new MultiFileEngine(TestFormats.Alpha, TestFormats.Beta)).ConvertAsync(Source.File(Input("book.alpha")), Target.Folder(Out, TestFormats.Beta)));

        Assert.Equal(ConversionFailureReason.FileExists, error.Reason);
        Assert.Equal(["book-sheet2.beta"], OutFiles());
        Assert.Equal("mine", File.ReadAllText(taken));
    }

    [Fact]
    public async Task OverwriteOn_ReplacesTheExistingFile()
    {
        Directory.CreateDirectory(Out);
        File.WriteAllText(Path.Combine(Out, "book-sheet2.beta"), "mine");

        await Make(new MultiFileEngine(TestFormats.Alpha, TestFormats.Beta), overwrite: true).ConvertAsync(Source.File(Input("book.alpha")), Target.Folder(Out, TestFormats.Beta));

        Assert.Equal(3, OutFiles().Length);
        Assert.NotEqual("mine", File.ReadAllText(Path.Combine(Out, "book-sheet2.beta")));
    }

    [Fact]
    public async Task ASubfolderWrittenByTheEngine_FailsTheWholeSet()
    {
        MultiFileEngine engine = new(TestFormats.Alpha, TestFormats.Beta) { WriteSubfolder = true };

        ConversionException error = await Assert.ThrowsAsync<ConversionException>(
            () => Make(engine).ConvertAsync(Source.File(Input("book.alpha")), Target.Folder(Out, TestFormats.Beta)));

        Assert.Equal(ConversionFailureReason.EngineFailed, error.Reason);
        Assert.Empty(OutFiles());
    }

    [Fact]
    public async Task Batch_ReportsEveryFileOfEveryInput_AndOtherPairsKeepTheirSingleFile()
    {
        string first = Input("one.alpha");
        string second = Input("two.alpha");

        GLAZOV.M.CONVERTER.Batch.BatchReport report = await Make(new MultiFileEngine(TestFormats.Alpha, TestFormats.Beta, files: 2))
            .ConvertBatchAsync([Source.File(first), Source.File(second)], Target.Folder(Out, TestFormats.Beta));

        Assert.Equal(2, report.Succeeded);
        Assert.Equal(["one-sheet1.beta", "one-sheet2.beta"], report.Items[0].OutputPaths.Select(Path.GetFileName));
        Assert.Equal(["two-sheet1.beta", "two-sheet2.beta"], report.Items[1].OutputPaths.Select(Path.GetFileName));
        Assert.Equal(4, OutFiles().Length);
    }

    [Fact]
    public async Task OrdinaryPair_IsUnchanged_OneFileNamedByTheInput()
    {
        LambdaEngine engine = new(null, [new ConversionPair(TestFormats.Alpha, TestFormats.Beta)]);

        ConversionResult result = await Make(engine).ConvertAsync(Source.File(Input("book.alpha")), Target.Folder(Out, TestFormats.Beta));

        Assert.Equal(["book.beta"], OutFiles());
        Assert.Equal([Path.Combine(Path.GetFullPath(Out), "book.beta")], result.OutputPaths.Select(Path.GetFullPath));
        Assert.Null(engine.Last!.OutputFolder);
    }

    [Fact]
    public void ThePairFlag_DoesNotChangeEquality()
    {
        ConversionPair plain = new(TestFormats.Alpha, TestFormats.Beta);
        ConversionPair flagged = new(TestFormats.Alpha, TestFormats.Beta) { WritesSeveralFiles = true };

        Assert.Equal(plain, flagged);
        Assert.Equal(plain.GetHashCode(), flagged.GetHashCode());
    }
}
