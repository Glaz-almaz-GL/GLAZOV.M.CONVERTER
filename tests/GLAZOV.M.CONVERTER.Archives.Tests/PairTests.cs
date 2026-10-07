using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Formats;

namespace GLAZOV.M.CONVERTER.Archives.Tests;

/// <summary>
/// Every pair between ZIP, TAR, TAR.GZ and GZip: entries, names in Russian, folders, an empty file, binary data and times come over, and the
/// result is read back by the standard library.
/// </summary>
public sealed class PairTests
{
    public static TheoryData<string, string> Pairs()
    {
        TheoryData<string, string> data = [];
        foreach (FileFormat from in ArchiveKit.Kinds)
        {
            foreach (FileFormat to in ArchiveKit.Kinds)
            {
                data.Add(from.Name, to.Name);
            }
        }

        return data;
    }

    private static FileFormat Kind(string name)
    {
        return ArchiveKit.Kinds.Single(kind => kind.Name == name);
    }

    [Theory]
    [MemberData(nameof(Pairs))]
    public async Task EveryPair_MovesTheEntriesWithTheirNamesContentAndTimes(string fromName, string toName)
    {
        FileFormat from = Kind(fromName);
        FileFormat to = Kind(toName);
        bool single = from.Equals(FileFormat.Gzip) || to.Equals(FileFormat.Gzip);
        IReadOnlyList<ArchiveSnapshot> entries = single ? ArchiveKit.Single() : ArchiveKit.Multi();
        byte[] source = ArchiveKit.Make(from, entries);

        byte[] result = await ArchiveKit.ConvertAsync(ArchiveKit.With(), source, to);

        List<ArchiveSnapshot> back = ArchiveKit.Read(to, result);
        Assert.Equal(entries.Count, back.Count);
        for (int i = 0; i < entries.Count; i++)
        {
            ArchiveSnapshot want = entries[i];
            ArchiveSnapshot got = back[i];
            Assert.Equal(want.IsDirectory, got.IsDirectory);
            Assert.Equal(want.Data, got.Data);
            if (!to.Equals(FileFormat.Gzip))
            {
                // a GZip source has no names or times of its own: the entry is named "data"
                Assert.Equal(from.Equals(FileFormat.Gzip) ? "data" : want.Name, got.Name);
                if (!from.Equals(FileFormat.Gzip))
                {
                    Assert.InRange(Math.Abs((got.Modified - want.Modified).TotalSeconds), 0, 2);
                }
            }
        }

        Assert.Equal(to, await ArchiveKit.With().DetectAsync(Source.Stream(new MemoryStream(result))));
    }

    [Fact]
    public void CatalogHoldsAllSixteenPairs_AndNeedsNoProgram()
    {
        Capabilities.ConverterCapabilities caps = ArchiveKit.With().Capabilities;

        Assert.Equal(16, caps.All.Count);
        Assert.All(caps.All, pair => Assert.Equal(Capabilities.ConversionAvailability.Ready, pair.Availability));
        Assert.All(ArchiveKit.Kinds, kind => Assert.Contains(kind, caps.Formats));
    }
}
