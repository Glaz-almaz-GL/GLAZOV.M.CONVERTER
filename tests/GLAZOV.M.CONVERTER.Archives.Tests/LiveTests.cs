using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Formats;

namespace GLAZOV.M.CONVERTER.Archives.Tests;

/// <summary>
/// What the engine writes is read by the program "tar" of the machine (TAR, TAR.GZ and ZIP). If the variable CONVERTER_RUN_KEEP names a folder,
/// the results are kept there for a look (the owner's folder is run-output/archives).
/// </summary>
public sealed class LiveTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "convtests-live-" + Guid.NewGuid().ToString("N"));

    public LiveTests()
    {
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
    }

    [TarToolFact]
    public async Task EveryResultThatTarCanRead_IsReadByTar_WithTheFolderAndThreeFiles()
    {
        string? keep = Environment.GetEnvironmentVariable("CONVERTER_RUN_KEEP");
        if (!string.IsNullOrWhiteSpace(keep))
        {
            Directory.CreateDirectory(keep);
        }

        foreach (FileFormat from in new[] { FileFormat.Zip, FileFormat.Tar, FileFormat.TarGz })
        {
            string source = Path.Combine(_folder, "исходный." + from.Extension);
            await File.WriteAllBytesAsync(source, ArchiveKit.Make(from, ArchiveKit.Multi()));
            foreach (FileFormat to in new[] { FileFormat.Zip, FileFormat.Tar, FileFormat.TarGz })
            {
                string output = Path.Combine(_folder, $"из-{from.Name}-в-{to.Name}.{to.Extension}");

                await ArchiveKit.With().ConvertAsync(Source.File(source), Target.File(output, to));

                int code = TarTool.List(output, out string listing);
                Assert.True(code == 0, $"tar did not read {Path.GetFileName(output)}");
                // The console of the machine may print the Russian letters in its own code page, so the names are checked by shape here;
                // the exact Russian names are checked by the standard library in PairTests.
                string[] lines = listing.Split((char)10, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                Assert.Equal(4, lines.Length);
                Assert.EndsWith("/", lines[0]);
                Assert.EndsWith(".txt", lines[1]);
                Assert.Equal(["empty.bin", "data.bin"], lines[2..]);
                if (!string.IsNullOrWhiteSpace(keep))
                {
                    File.Copy(output, Path.Combine(keep, Path.GetFileName(output)), overwrite: true);
                }
            }
        }
    }
}
