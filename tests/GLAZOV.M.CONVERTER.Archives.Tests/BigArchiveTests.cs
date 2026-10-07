using System.Diagnostics;
using System.Formats.Tar;
using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Formats;

namespace GLAZOV.M.CONVERTER.Archives.Tests;

/// <summary>
/// An archive of several hundred megabytes goes through in a stream: the managed memory does not grow with the size of the archive.
/// </summary>
public sealed class BigArchiveTests : IDisposable
{
    private const long Size = 320L * 1024 * 1024;

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "convtests-big-" + Guid.NewGuid().ToString("N"));

    public BigArchiveTests()
    {
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
    }

    private async Task<string> BigTarAsync()
    {
        string raw = Path.Combine(_folder, "raw.bin");
        await using (FileStream file = new(raw, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20, FileOptions.Asynchronous))
        {
            byte[] chunk = new byte[1 << 20];
            Random random = new(5);
            for (long written = 0; written < Size; written += chunk.Length)
            {
                random.NextBytes(chunk);
                await file.WriteAsync(chunk);
            }
        }

        string tar = Path.Combine(_folder, "big.tar");
        await using (FileStream output = new(tar, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20, FileOptions.Asynchronous))
        await using (TarWriter writer = new(output, TarEntryFormat.Pax, leaveOpen: true))
        await using (FileStream data = new(raw, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20, FileOptions.Asynchronous))
        {
            await writer.WriteEntryAsync(new PaxTarEntry(TarEntryType.RegularFile, "big.bin") { DataStream = data });
        }

        File.Delete(raw);
        return tar;
    }

    private static async Task<long> PeakGrowthAsync(Func<Task> work)
    {
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

        await work();
        await stop.CancelAsync();
        await watcher;
        return peak - baseline;
    }

    [Fact]
    public async Task TarOfSeveralHundredMegabytes_GoesToZipAndBackToTar_WithoutGrowingTheMemory()
    {
        string tar = await BigTarAsync();
        string zip = Path.Combine(_folder, "big.zip");
        string again = Path.Combine(_folder, "again.tar");
        Converter converter = ArchiveKit.With(new ArchivesSettings { CompressionLevel = System.IO.Compression.CompressionLevel.Fastest });

        long growth = await PeakGrowthAsync(async () =>
        {
            await converter.ConvertAsync(Source.File(tar), Target.File(zip, FileFormat.Zip));
            await converter.ConvertAsync(Source.File(zip), Target.File(again, FileFormat.Tar));
        });

        Assert.True(growth < 96L * 1024 * 1024, $"the managed memory grew by {growth / (1024 * 1024)} MB for an archive of {Size / (1024 * 1024)} MB");
        using FileStream first = File.OpenRead(tar);
        using FileStream second = File.OpenRead(again);
        using TarReader a = new(first);
        using TarReader b = new(second);
        TarEntry one = a.GetNextEntry()!;
        TarEntry two = b.GetNextEntry()!;
        Assert.Equal(one.Length, two.Length);
        Assert.Equal(Size, two.Length);
        byte[] x = new byte[1 << 20];
        byte[] y = new byte[1 << 20];
        long compared = 0;
        int read;
        while ((read = await one.DataStream!.ReadAsync(x)) > 0)
        {
            await two.DataStream!.ReadExactlyAsync(y.AsMemory(0, read));
            Assert.True(x.AsSpan(0, read).SequenceEqual(y.AsSpan(0, read)), $"the content differs after {compared} bytes");
            compared += read;
        }

        Assert.Equal(Size, compared);
    }
}
