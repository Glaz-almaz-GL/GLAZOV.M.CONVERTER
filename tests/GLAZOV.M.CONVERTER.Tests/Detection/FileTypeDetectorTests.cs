using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Detection;
using GLAZOV.M.CONVERTER.Errors;
using GLAZOV.M.CONVERTER.IO;

namespace GLAZOV.M.CONVERTER.Tests.Detection;

/// <summary>
/// Публичное определение типа по файлу и потоку: по содержимому, а не по имени; испорченные входы — понятные ошибки.
/// </summary>
public sealed class FileTypeDetectorTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "convtests-" + Guid.NewGuid().ToString("N"));

    public FileTypeDetectorTests()
    {
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
    }

    private static byte[] Sample(string name)
    {
        return File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Samples", name));
    }

    private string Put(string name, byte[] data)
    {
        string path = Path.Combine(_folder, name);
        File.WriteAllBytes(path, data);
        return path;
    }

    [Theory]
    [InlineData("sample-id3.mp3", "MP3")]
    [InlineData("sample-raw.mp3", "MP3")]
    [InlineData("sample.wav", "WAV")]
    [InlineData("sample-vorbis.ogg", "OGG")]
    [InlineData("sample-opus.ogg", "OGG")]
    public async Task File_OfEachFormat_IsDetected(string sample, string expected)
    {
        string path = Put(sample, Sample(sample));

        FileFormat type = await FileTypeDetector.DetectAsync(Source.File(path));

        Assert.Equal(expected, type.Name);
    }

    [Fact]
    public async Task File_WithForeignExtension_IsDetectedByContent()
    {
        string path = Put("это-не-ogg.ogg", Sample("sample-id3.mp3"));

        FileFormat type = await FileTypeDetector.DetectAsync(Source.File(path));

        Assert.Same(FileFormat.Mp3, type);
    }

    [Fact]
    public async Task File_ThatIsNotAudio_IsText_BinaryJunkAndEmptyFile_AreUnknown()
    {
        string text = Put("текст.mp3", "Это текст, а не звук."u8.ToArray());
        string junk = Put("мусор.mp3", [0x00, 0x01, 0x02, 0x03, 0x9F, 0xFF, 0x10]);
        string empty = Put("пусто.wav", []);

        Assert.Same(FileFormat.Text, await FileTypeDetector.DetectAsync(Source.File(text)));
        Assert.Same(FileFormat.Unknown, await FileTypeDetector.DetectAsync(Source.File(junk)));
        Assert.Same(FileFormat.Unknown, await FileTypeDetector.DetectAsync(Source.File(empty)));
    }

    [Fact]
    public async Task SeekableStream_IsDetected_AndPositionIsRestored()
    {
        byte[] data = Sample("sample.wav");
        using MemoryStream stream = new(data) { Position = 0 };

        FileFormat type = await FileTypeDetector.DetectAsync(Source.Stream(stream));

        Assert.Same(FileFormat.Wav, type);
        Assert.Equal(0, stream.Position);
        Assert.True(stream.CanRead);
    }

    [Fact]
    public async Task SeekableStream_FromTheMiddle_IsMeasuredFromWhereItStands()
    {
        byte[] data = [.. new byte[7], .. Sample("sample-vorbis.ogg")];
        using MemoryStream stream = new(data) { Position = 7 };

        FileFormat type = await FileTypeDetector.DetectAsync(Source.Stream(stream));

        Assert.Same(FileFormat.Ogg, type);
        Assert.Equal(7, stream.Position);
    }

    [Fact]
    public async Task StreamThatCannotSeek_IsRefusedWithAnExplanation_NothingIsRead()
    {
        using ConsumeCountingStream stream = new(Sample("sample.wav"));

        ArgumentException ex = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await FileTypeDetector.DetectAsync(Source.Stream(stream)));

        Assert.Contains("перематывать", ex.Message);
        Assert.Equal(0, stream.BytesRead);
    }

    [Fact]
    public async Task Folder_IsAUsageError()
    {
        await Assert.ThrowsAsync<ArgumentException>(async () => await FileTypeDetector.DetectAsync(Source.Folder(_folder)));
    }

    [Fact]
    public async Task MissingFile_IsInputUnreadable_AtTheDetectionStage()
    {
        ConversionException ex = await Assert.ThrowsAsync<ConversionException>(async () =>
            await FileTypeDetector.DetectAsync(Source.File(Path.Combine(_folder, "нет-такого.mp3"))));

        Assert.Equal(ConversionStage.Detection, ex.Stage);
        Assert.Equal(ConversionFailureReason.InputUnreadable, ex.Reason);
        Assert.Contains("нет-такого.mp3", ex.Message);
    }

    [Fact]
    public async Task Cancelled_ThrowsOperationCanceled()
    {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();
        string path = Put("c.wav", Sample("sample.wav"));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await FileTypeDetector.DetectAsync(Source.File(path), cts.Token));
    }

    [Fact]
    public async Task OpenHandle_NonSeekableStream_DetectedAndStillReadableWhole()
    {
        byte[] data = Sample("sample-opus.ogg");
        using ConsumeCountingStream stream = new(data);

        await using InputHandle handle = await InputReader.OpenAsync(Source.Stream(stream));
        FileFormat type = await FileTypeDetector.DetectAsync(handle);
        using MemoryStream copy = new();
        await handle.Stream.CopyToAsync(copy);

        Assert.Same(FileFormat.Ogg, type);
        Assert.Equal(data, copy.ToArray());
    }

    private sealed class ConsumeCountingStream(byte[] data) : Stream
    {
        private readonly MemoryStream _inner = new(data);

        public int BytesRead { get; private set; }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int read = _inner.Read(buffer, offset, count);
            BytesRead += read;
            return read;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }
}
