using System.Formats.Tar;
using System.IO.Compression;
using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Formats;
using GLAZOV.M.CONVERTER.Settings;
using SharpCompress.Compressors;
using SharpCompress.Compressors.BZip2;

namespace GLAZOV.M.CONVERTER.ArchivesExtra.Tests;

/// <summary>
/// Helpers of the tests of the extra archives: a converter with the extension, the samples, reading an archive back.
/// </summary>
internal static class ExtraKit
{
    internal static readonly DateTimeOffset Stamp = new(2024, 5, 6, 7, 8, 10, TimeSpan.Zero);

    internal static string Sample(string name)
    {
        return Path.Combine(AppContext.BaseDirectory, "Samples", name);
    }

    internal static byte[] Bytes(string name)
    {
        return File.ReadAllBytes(Sample(name));
    }

    internal static Converter With(ArchivesExtraSettings? settings = null, bool withBase = false)
    {
        ConverterSettings converterSettings = new();
        converterSettings.Extensions.Add(new FormatsExtension());
        converterSettings.Extensions.Add(settings is null ? new ArchivesExtraExtension() : new ArchivesExtraExtension(settings));
        if (withBase)
        {
            converterSettings.Extensions.Add(new Archives.ArchivesExtension());
        }

        return new Converter(converterSettings);
    }

    /// <summary>
    /// The names the samples carry: Russian for all but the RAR sample, whose names are plain ASCII.
    /// </summary>
    internal static string[] Names(bool russian)
    {
        return russian ? ["папка/", "папка/привет.txt", "empty.bin", "data.bin"] : ["folder/", "folder/hello.txt", "empty.bin", "data.bin"];
    }

    internal static byte[] Pattern()
    {
        return [.. Enumerable.Range(0, 100_000).Select(i => (byte)((i * 31) + 7))];
    }

    internal static byte[] Hello()
    {
        return System.Text.Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("Привет, мир! ", 100)));
    }

    /// <summary>
    /// Reads back what the engine wrote, with the standard library (BZip2 with SharpCompress, which the engine also uses to write it).
    /// </summary>
    internal static List<(string Name, bool IsDirectory, byte[] Data)> Read(FileFormat kind, byte[] data)
    {
        List<(string, bool, byte[])> result = [];
        using MemoryStream stream = new(data);
        if (kind.Equals(FileFormat.Zip))
        {
            using System.IO.Compression.ZipArchive zip = new(stream, ZipArchiveMode.Read);
            foreach (ZipArchiveEntry entry in zip.Entries)
            {
                using Stream content = entry.Open();
                using MemoryStream copy = new();
                content.CopyTo(copy);
                result.Add((entry.FullName, entry.FullName.EndsWith('/'), copy.ToArray()));
            }

            return result;
        }

        if (kind.Equals(FileFormat.Gzip) || kind.Equals(ArchivesExtraFormats.Bzip2))
        {
            using Stream unpacked = kind.Equals(FileFormat.Gzip)
                ? new GZipStream(stream, System.IO.Compression.CompressionMode.Decompress)
                : new BZip2Stream(stream, SharpCompress.Compressors.CompressionMode.Decompress, false);
            using MemoryStream copy = new();
            unpacked.CopyTo(copy);
            return [("?", false, copy.ToArray())];
        }

        using Stream source = kind.Equals(FileFormat.TarGz)
            ? new GZipStream(stream, System.IO.Compression.CompressionMode.Decompress)
            : kind.Equals(ArchivesExtraFormats.TarBz2) ? new BZip2Stream(stream, SharpCompress.Compressors.CompressionMode.Decompress, false) : stream;
        using TarReader tar = new(source);
        while (tar.GetNextEntry() is { } entry)
        {
            using MemoryStream copy = new();
            entry.DataStream?.CopyTo(copy);
            result.Add((entry.Name, entry.EntryType == TarEntryType.Directory, copy.ToArray()));
        }

        return result;
    }
}
