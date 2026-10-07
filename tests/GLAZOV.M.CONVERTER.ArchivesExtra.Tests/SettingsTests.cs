using System.IO.Compression;
using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Formats;

namespace GLAZOV.M.CONVERTER.ArchivesExtra.Tests;

/// <summary>
/// The settings of the package: the same limits as the base package, the checks of values, the copy taken when the extension is created.
/// </summary>
public sealed class SettingsTests
{
    [Fact]
    public void Defaults_AreTheSameAsThoseOfTheBasePackage()
    {
        ArchivesExtraSettings extra = new();
        Archives.ArchivesSettings basic = new();

        Assert.Equal(
            (basic.MaxEntries, basic.MaxTotalBytes, basic.MaxEntryBytes, basic.MaxCompressionRatio, basic.CompressionLevel, basic.ZipNameCodePage),
            (extra.MaxEntries, extra.MaxTotalBytes, extra.MaxEntryBytes, extra.MaxCompressionRatio, extra.CompressionLevel, extra.ZipNameCodePage));
        Assert.Equal(CompressionLevel.Optimal, extra.CompressionLevel);
    }

    [Fact]
    public void WrongValues_AreRefusedAtOnce()
    {
        ArchivesExtraSettings settings = new();

        Assert.Throws<ArgumentOutOfRangeException>(() => settings.MaxEntries = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => settings.MaxTotalBytes = -1);
        Assert.Throws<ArgumentOutOfRangeException>(() => settings.MaxEntryBytes = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => settings.MaxCompressionRatio = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => settings.ZipNameCodePage = 99999);
    }

    [Fact]
    public async Task TheExtension_KeepsACopy_LaterChangesDoNotReachIt()
    {
        ArchivesExtraSettings settings = new() { MaxEntries = 100 };
        Converter converter = ExtraKit.With(settings);
        settings.MaxEntries = 1;

        ConversionResult result = await converter.ConvertAsync(Source.File(ExtraKit.Sample("sample.7z")), Target.Memory(FileFormat.Tar));

        Assert.True(result.Succeeded);
    }
}
