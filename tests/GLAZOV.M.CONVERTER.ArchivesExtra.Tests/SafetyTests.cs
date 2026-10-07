using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Errors;
using GLAZOV.M.CONVERTER.Formats;

namespace GLAZOV.M.CONVERTER.ArchivesExtra.Tests;

/// <summary>
/// What the engine refuses on the extra kinds: dangerous names, encrypted entries, bombs, limits, cut and broken files. No result file is left behind.
/// </summary>
public sealed class SafetyTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "convtests-extra-" + Guid.NewGuid().ToString("N"));

    public SafetyTests()
    {
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
    }

    private async Task<ConversionException> RefusedAsync(Converter converter, string inputName, byte[] bytes, FileFormat to)
    {
        string input = Path.Combine(_folder, inputName);
        string output = Path.Combine(_folder, "result." + to.Extension);
        await File.WriteAllBytesAsync(input, bytes);

        ConversionException error = await Assert.ThrowsAsync<ConversionException>(async () =>
            await converter.ConvertAsync(Source.File(input), Target.File(output, to)));

        Assert.False(File.Exists(output), "a refused archive left a result file");
        Assert.Equal(ConversionFailureReason.EngineFailed, error.Reason);
        return error;
    }

    [Fact]
    public async Task DangerousNameInRar_IsRefused()
    {
        ConversionException error = await RefusedAsync(ExtraKit.With(), "evil.rar", ExtraKit.Bytes("sample-evil.rar"), FileFormat.Zip);

        Assert.Contains("Опасная запись", error.Message);
    }

    [Fact]
    public async Task EncryptedEntryInRar_IsRefused()
    {
        ConversionException error = await RefusedAsync(ExtraKit.With(), "secret.rar", ExtraKit.Bytes("sample-encrypted.rar"), FileFormat.Zip);

        Assert.Contains("зашифрован", error.Message);
    }

    [Theory]
    [InlineData("sample-bomb.bz2", "bomb.bz2")]
    [InlineData("sample-bomb.xz", "bomb.xz")]
    public async Task ArchiveBomb_IsRefusedByTheRatio_AndTheMessageNamesTheSetting(string sample, string name)
    {
        ConversionException error = await RefusedAsync(ExtraKit.With(), name, ExtraKit.Bytes(sample), FileFormat.Zip);

        Assert.Contains("архив-бомбу", error.Message);
        Assert.Contains("MaxCompressionRatio", error.Message);
    }

    [Fact]
    public async Task EveryLimit_NamesItsSetting()
    {
        byte[] sevenZip = ExtraKit.Bytes("sample.7z");

        ConversionException entries = await RefusedAsync(ExtraKit.With(new ArchivesExtraSettings { MaxEntries = 2 }), "a.7z", sevenZip, FileFormat.Tar);
        ConversionException entry = await RefusedAsync(ExtraKit.With(new ArchivesExtraSettings { MaxEntryBytes = 1000 }), "b.7z", sevenZip, FileFormat.Tar);
        ConversionException total = await RefusedAsync(ExtraKit.With(new ArchivesExtraSettings { MaxTotalBytes = 1000 }), "c.7z", sevenZip, FileFormat.Tar);

        Assert.Contains("MaxEntries", entries.Message);
        Assert.Contains("MaxEntryBytes", entry.Message);
        Assert.Contains("MaxTotalBytes", total.Message);
    }

    [Theory]
    [InlineData("sample.7z", "cut.7z")]
    [InlineData("sample.tar.xz", "cut.tar.xz")]
    [InlineData("sample.tar.bz2", "cut.tar.bz2")]
    [InlineData("sample.xz", "cut.xz")]
    [InlineData("sample.bz2", "cut.bz2")]
    public async Task CutFile_IsRefusedAsBroken_NotAsAHalfArchive(string sample, string name)
    {
        byte[] whole = ExtraKit.Bytes(sample);

        ConversionException error = await RefusedAsync(ExtraKit.With(), name, whole.AsSpan(0, whole.Length / 2).ToArray(), FileFormat.Zip);

        Assert.True(
            error.Message.Contains("оборван", StringComparison.OrdinalIgnoreCase) || error.Message.Contains("повреждён", StringComparison.OrdinalIgnoreCase),
            error.Message);
    }

    [Fact]
    public async Task SevenZipWithSpoiledData_IsRefusedAsBroken()
    {
        byte[] bytes = ExtraKit.Bytes("sample.7z");
        for (int i = 40; i < 300; i++)
        {
            bytes[i] ^= 0xA5;
        }

        ConversionException error = await RefusedAsync(ExtraKit.With(), "spoiled.7z", bytes, FileFormat.Zip);

        Assert.True(error.Message.Contains("повреждён", StringComparison.Ordinal), error.Message);
    }

    [Fact]
    public async Task SeveralEntriesDoNotFitIntoBzip2_AndLeaveNoFile()
    {
        ConversionException error = await RefusedAsync(ExtraKit.With(), "many.7z", ExtraKit.Bytes("sample.7z"), ArchivesExtraFormats.Bzip2);

        Assert.Contains("BZip2", error.Message);
    }
}
