using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Formats;

namespace GLAZOV.M.CONVERTER.Archives.Tests;

/// <summary>
/// Input and result as streams without seeking, files, memory and the progress.
/// </summary>
public sealed class StreamTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "convtests-stream-" + Guid.NewGuid().ToString("N"));

    public StreamTests()
    {
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
    }

    [Theory]
    [InlineData("ZIP")]
    [InlineData("TAR")]
    [InlineData("TAR.GZ")]
    public async Task InputThatCannotSeek_AndAResultThatIsAStream_Work(string kindName)
    {
        FileFormat kind = ArchiveKit.Kinds.Single(k => k.Name == kindName);
        // A ZIP that cannot be read back is told from a document only if the whole file lies in the detection window: keep it small here.
        IReadOnlyList<ArchiveSnapshot> entries = kind.Equals(FileFormat.Zip) ? ArchiveKit.Single() : ArchiveKit.Multi();
        byte[] source = ArchiveKit.Make(kind, entries);
        await using NonSeekableStream input = new(new MemoryStream(source));
        using MemoryStream output = new();

        await ArchiveKit.With().ConvertAsync(Source.Stream(input), Target.Stream(output, FileFormat.TarGz));

        Assert.Equal(entries.Select(e => e.Name), ArchiveKit.Read(FileFormat.TarGz, output.ToArray()).Select(e => e.Name));
    }

    [Fact]
    public async Task BigZipFromAStreamThatCannotSeek_IsNotConfirmed_AndTheSameZipAsAFileIs()
    {
        byte[] zip = ArchiveKit.Make(FileFormat.Zip, ArchiveKit.Multi());
        await using NonSeekableStream input = new(new MemoryStream(zip));
        string path = Path.Combine(_folder, "big.zip");
        await File.WriteAllBytesAsync(path, zip);

        Errors.ConversionException error = await Assert.ThrowsAsync<Errors.ConversionException>(async () =>
            await ArchiveKit.With().ConvertAsync(Source.Stream(input), Target.Memory(FileFormat.Tar)));
        ConversionResult fromFile = await ArchiveKit.With().ConvertAsync(Source.File(path), Target.Memory(FileFormat.Tar));

        Assert.Equal(Errors.ConversionFailureReason.TypeNotConfirmed, error.Reason);
        Assert.True(fromFile.Succeeded);
    }

    [Fact]
    public async Task ZipFile_ToTarFile_AndBack_KeepsEverything()
    {
        string zip = Path.Combine(_folder, "a.zip");
        string tar = Path.Combine(_folder, "a.tar");
        string again = Path.Combine(_folder, "b.zip");
        await File.WriteAllBytesAsync(zip, ArchiveKit.Make(FileFormat.Zip, ArchiveKit.Multi()));

        await ArchiveKit.With().ConvertAsync(Source.File(zip), Target.File(tar, FileFormat.Tar));
        await ArchiveKit.With().ConvertAsync(Source.File(tar), Target.File(again, FileFormat.Zip));

        Assert.Equal(
            ArchiveKit.Multi().Select(e => (e.Name, e.Data.Length)),
            ArchiveKit.Read(FileFormat.Zip, await File.ReadAllBytesAsync(again)).Select(e => (e.Name, e.Data.Length)));
    }

    [Fact]
    public async Task Progress_GrowsAndEndsAtOneHundred()
    {
        string zip = Path.Combine(_folder, "p.zip");
        await File.WriteAllBytesAsync(zip, ArchiveKit.Make(FileFormat.Zip, ArchiveKit.Multi()));
        List<int> seen = [];

        await ArchiveKit.With().ConvertAsync(Source.File(zip), Target.File(Path.Combine(_folder, "p.tar"), FileFormat.Tar), progress: new InlineProgress(seen.Add));

        Assert.Equal(100, seen[^1]);
        Assert.Equal(seen.Order(), seen);
    }

    private sealed class InlineProgress(Action<int> onPercent) : IProgress<ConversionProgress>
    {
        public void Report(ConversionProgress value)
        {
            onPercent((int)Math.Round(value.Percent));
        }
    }
}
