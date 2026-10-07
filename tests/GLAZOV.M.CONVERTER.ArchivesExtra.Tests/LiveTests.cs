using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Formats;

namespace GLAZOV.M.CONVERTER.ArchivesExtra.Tests;

/// <summary>
/// What the engine writes (TAR.BZ2 and ZIP from the extra kinds) is read by bsdtar, and the samples made by other programs are listed by it too,
/// as a witness that they are real. If CONVERTER_RUN_KEEP names a folder the results are kept there for a look (the owner's folder is run-output/archives).
/// </summary>
public sealed class LiveTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "convtests-livex-" + Guid.NewGuid().ToString("N"));

    public LiveTests()
    {
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
    }

    [BsdTarFact]
    public async Task EveryExtraInput_ToTarBz2AndZip_IsListedByBsdTar()
    {
        string? keep = Environment.GetEnvironmentVariable("CONVERTER_RUN_KEEP");
        if (!string.IsNullOrWhiteSpace(keep))
        {
            Directory.CreateDirectory(keep);
        }

        foreach (string sample in new[] { "sample.7z", "sample.rar", "sample.tar.xz", "sample.tar.bz2" })
        {
            Assert.Equal(0, BsdTar.Run(_folder, "-tf", ExtraKit.Sample(sample)));
            foreach (FileFormat to in new[] { ArchivesExtraFormats.TarBz2, FileFormat.Zip })
            {
                string output = Path.Combine(_folder, $"из-{sample.Replace('.', '_')}-в-{to.Name}.{to.Extension}");

                await ExtraKit.With().ConvertAsync(Source.File(ExtraKit.Sample(sample)), Target.File(output, to));

                Assert.Equal(0, BsdTar.Run(_folder, "-tf", output));
                if (!string.IsNullOrWhiteSpace(keep))
                {
                    File.Copy(output, Path.Combine(keep, Path.GetFileName(output)), overwrite: true);
                }
            }
        }
    }
}
