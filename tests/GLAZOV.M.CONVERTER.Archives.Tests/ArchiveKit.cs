using System.Formats.Tar;
using System.IO.Compression;
using System.Text;
using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Formats;
using GLAZOV.M.CONVERTER.Settings;

namespace GLAZOV.M.CONVERTER.Archives.Tests;

/// <summary>
/// Helpers of the archive tests: a converter with the extension, archives built and read with the standard library (independent of the engine).
/// </summary>
internal static class ArchiveKit
{
    internal static readonly FileFormat[] Kinds = [FileFormat.Zip, FileFormat.Tar, FileFormat.TarGz, FileFormat.Gzip];

    internal static readonly DateTimeOffset Stamp = new(2024, 5, 6, 7, 8, 10, TimeSpan.Zero);

    internal static Converter With(ArchivesSettings? settings = null)
    {
        ConverterSettings converterSettings = new();
        converterSettings.Extensions.Add(new FormatsExtension());
        converterSettings.Extensions.Add(settings is null ? new ArchivesExtension() : new ArchivesExtension(settings));
        return new Converter(converterSettings);
    }

    /// <summary>
    /// The entries of the usual test archive: a folder, a file with Russian text in a folder with a Russian name, an empty file, 100 000 random bytes.
    /// </summary>
    internal static IReadOnlyList<ArchiveSnapshot> Multi()
    {
        byte[] random = new byte[100_000];
        new Random(11).NextBytes(random);
        return
        [
            new ArchiveSnapshot("папка/", true, [], Stamp),
            new ArchiveSnapshot("папка/привет.txt", false, Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("Привет, мир! ", 100))), Stamp),
            new ArchiveSnapshot("empty.bin", false, [], Stamp),
            new ArchiveSnapshot("data.bin", false, random, Stamp),
        ];
    }

    /// <summary>
    /// One file only: what a GZip holds.
    /// </summary>
    internal static IReadOnlyList<ArchiveSnapshot> Single()
    {
        return [new ArchiveSnapshot("data.bin", false, Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("One file. ", 500))), Stamp)];
    }

    internal static byte[] Make(FileFormat kind, IReadOnlyList<ArchiveSnapshot> entries)
    {
        using MemoryStream stream = new();
        if (kind.Equals(FileFormat.Zip))
        {
            using ZipArchive zip = new(stream, ZipArchiveMode.Create, leaveOpen: true);
            foreach (ArchiveSnapshot entry in entries)
            {
                ZipArchiveEntry created = zip.CreateEntry(entry.Name);
                created.LastWriteTime = entry.Modified.ToLocalTime();
                if (!entry.IsDirectory)
                {
                    using Stream content = created.Open();
                    content.Write(entry.Data);
                }
            }

            zip.Dispose();
            return stream.ToArray();
        }

        if (kind.Equals(FileFormat.Gzip))
        {
            using GZipStream gzip = new(stream, CompressionLevel.Optimal, leaveOpen: true);
            gzip.Write(entries.Single(e => !e.IsDirectory).Data);
            gzip.Dispose();
            return stream.ToArray();
        }

        Stream target = kind.Equals(FileFormat.TarGz) ? new GZipStream(stream, CompressionLevel.Optimal, leaveOpen: true) : stream;
        using (target)
        using (TarWriter tar = new(target, TarEntryFormat.Pax, leaveOpen: true))
        {
            foreach (ArchiveSnapshot entry in entries)
            {
                PaxTarEntry created = new(entry.IsDirectory ? TarEntryType.Directory : TarEntryType.RegularFile, entry.Name) { ModificationTime = entry.Modified };
                if (!entry.IsDirectory)
                {
                    created.DataStream = new MemoryStream(entry.Data);
                }

                tar.WriteEntry(created);
            }
        }

        return stream.ToArray();
    }

    /// <summary>
    /// Reads an archive back with the standard library.
    /// </summary>
    internal static List<ArchiveSnapshot> Read(FileFormat kind, byte[] data)
    {
        List<ArchiveSnapshot> result = [];
        using MemoryStream stream = new(data);
        if (kind.Equals(FileFormat.Zip))
        {
            using ZipArchive zip = new(stream, ZipArchiveMode.Read);
            foreach (ZipArchiveEntry entry in zip.Entries)
            {
                using Stream content = entry.Open();
                using MemoryStream copy = new();
                content.CopyTo(copy);
                result.Add(new ArchiveSnapshot(entry.FullName, entry.FullName.EndsWith('/'), copy.ToArray(), entry.LastWriteTime));
            }

            return result;
        }

        if (kind.Equals(FileFormat.Gzip))
        {
            using GZipStream gzip = new(stream, CompressionMode.Decompress);
            using MemoryStream copy = new();
            gzip.CopyTo(copy);
            return [new ArchiveSnapshot("?", false, copy.ToArray(), DateTimeOffset.MinValue)];
        }

        using Stream source = kind.Equals(FileFormat.TarGz) ? new GZipStream(stream, CompressionMode.Decompress) : stream;
        using TarReader tar = new(source);
        while (tar.GetNextEntry() is { } entry)
        {
            using MemoryStream copy = new();
            entry.DataStream?.CopyTo(copy);
            bool directory = entry.EntryType == TarEntryType.Directory;
            result.Add(new ArchiveSnapshot(entry.Name, directory, copy.ToArray(), entry.ModificationTime));
        }

        return result;
    }

    internal static async Task<byte[]> ConvertAsync(Converter converter, byte[] source, FileFormat target)
    {
        using MemoryStream input = new(source);
        ConversionResult result = await converter.ConvertAsync(Source.Stream(input), Target.Memory(target));
        Assert.True(result.Succeeded);
        return result.Bytes!;
    }
}
