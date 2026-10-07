using System.Security.Cryptography;
using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Formats;

namespace GLAZOV.M.CONVERTER.ArchivesExtra.Tests;

/// <summary>
/// A 7z of several hundred megabytes (several files in one block, as 7-Zip makes it) goes through in a stream: the managed memory does not grow with the size.
/// </summary>
public sealed class BigArchiveTests : IDisposable
{
    private const int Files = 3;
    private const long FileSize = 100L * 1024 * 1024;

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "convtests-bigx-" + Guid.NewGuid().ToString("N"));

    public BigArchiveTests()
    {
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
    }

    [BsdTarFact]
    public async Task SevenZipOfThreeBigFiles_GoesToZip_WithoutGrowingTheMemory_AndTheContentIsTheSame()
    {
        string[] hashes = new string[Files];
        string source = Path.Combine(_folder, "src");
        Directory.CreateDirectory(source);
        for (int i = 0; i < Files; i++)
        {
            await using FileStream file = new(Path.Combine(source, $"file{i}.bin"), FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20, FileOptions.Asynchronous);
            using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            byte[] chunk = new byte[1 << 20];
            Random random = new(100 + i);
            for (long written = 0; written < FileSize; written += chunk.Length)
            {
                random.NextBytes(chunk);
                hash.AppendData(chunk);
                await file.WriteAsync(chunk);
            }

            hashes[i] = Convert.ToHexString(hash.GetHashAndReset());
        }

        string sevenZip = Path.Combine(_folder, "big.7z");
        Assert.Equal(0, BsdTar.Run(_folder, "--format", "7zip", "--options", "7zip:compression=copy", "-cf", sevenZip, "-C", source, "file0.bin", "file1.bin", "file2.bin"));
        Directory.Delete(source, recursive: true);
        string zip = Path.Combine(_folder, "big.zip");
        Converter converter = ExtraKit.With(new ArchivesExtraSettings { CompressionLevel = System.IO.Compression.CompressionLevel.Fastest });

        GC.Collect();
        long baseline = GC.GetTotalMemory(forceFullCollection: true);
        long peak = baseline;
        using CancellationTokenSource stop = new();
        Task watcher = Task.Run(async () =>
        {
            while (!stop.IsCancellationRequested)
            {
                peak = Math.Max(peak, GC.GetTotalMemory(forceFullCollection: false));
                await Task.Delay(10);
            }
        });
        await converter.ConvertAsync(Source.File(sevenZip), Target.File(zip, FileFormat.Zip));
        await stop.CancelAsync();
        await watcher;

        long growth = peak - baseline;
        Assert.True(growth < 96L * 1024 * 1024, $"the managed memory grew by {growth / (1024 * 1024)} MB for an archive of {Files * FileSize / (1024 * 1024)} MB");
        using System.IO.Compression.ZipArchive archive = System.IO.Compression.ZipFile.OpenRead(zip);
        Assert.Equal(Files, archive.Entries.Count);
        for (int i = 0; i < Files; i++)
        {
            System.IO.Compression.ZipArchiveEntry entry = archive.GetEntry($"file{i}.bin")!;
            Assert.Equal(FileSize, entry.Length);
            await using Stream content = entry.Open();
            Assert.Equal(hashes[i], Convert.ToHexString(await SHA256.HashDataAsync(content)));
        }
    }
}
