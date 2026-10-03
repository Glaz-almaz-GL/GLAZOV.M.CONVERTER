using System.Diagnostics;
using System.Globalization;
using GLAZOV.M.CONVERTER.Batch;
using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Detection;
using GLAZOV.M.CONVERTER.Errors;
using GLAZOV.M.CONVERTER.Settings;

namespace GLAZOV.M.CONVERTER.Tests.Live;

/// <summary>
/// Прогон звука на настоящих файлах через публичные вызовы эталона и настоящий FFmpeg: все пары форматов,
/// файл, поток, память, папка параллельно, отмена, параметры звука, испорченный и «лживый» вход.
/// Результаты читаются обратно настоящим ffprobe. Если переменная CONVERTER_RUN_KEEP указывает папку —
/// результаты пар сохраняются туда для прослушивания. Нет FFmpeg — проверки пропускаются, а не проходят.
/// </summary>
public sealed class AudioRunTests(AudioFixture fixture) : IClassFixture<AudioFixture>, IDisposable
{
    private static readonly FileFormat[] Targets = [FileFormat.Mp3, FileFormat.Wav, FileFormat.Ogg];

    private readonly string _work = Path.Combine(Path.GetTempPath(), "convrun-out-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_work))
        {
            Directory.Delete(_work, recursive: true);
        }
    }

    private string Out(string name)
    {
        Directory.CreateDirectory(_work);
        return Path.Combine(_work, name);
    }

    private static void Keep(string produced, string label)
    {
        string? keep = Environment.GetEnvironmentVariable("CONVERTER_RUN_KEEP");
        if (string.IsNullOrWhiteSpace(keep))
        {
            return;
        }

        Directory.CreateDirectory(keep);
        File.Copy(produced, Path.Combine(keep, label + Path.GetExtension(produced)), overwrite: true);
    }

    private static string Label(AudioFixture.Input input, FileFormat to)
    {
        return Path.GetFileNameWithoutExtension(input.Path) + "-в-" + to.Name;
    }

    private async Task AssertDurationAsync(string path, double expected)
    {
        Dictionary<string, string> probe = await fixture.ProbeFileAsync(path);
        double seconds = double.Parse(probe["duration"], CultureInfo.InvariantCulture);
        Assert.True(seconds >= expected - 0.2 && seconds <= expected + 0.3, $"{Path.GetFileName(path)}: длительность {seconds:F3} с вместо {expected} с");
    }

    [ExternalFact]
    public async Task EveryRealInput_ToEveryFormat_File_ToFile()
    {
        Converter converter = new();
        int done = 0;

        foreach (AudioFixture.Input input in fixture.Inputs)
        {
            foreach (FileFormat to in Targets)
            {
                string output = Out($"{Label(input, to)}.{to.Extension}");

                ConversionResult result = await converter.ConvertAsync(Source.File(input.Path), Target.File(output, to));

                Assert.True(result.Succeeded);
                Assert.Equal(input.TypeName, result.SourceType.Name);
                Assert.Same(to, await FileTypeDetector.DetectAsync(Source.File(output)));
                await AssertDurationAsync(output, input.Seconds);
                Keep(output, Label(input, to));
                done++;
            }
        }

        Assert.Equal(24, done);
    }

    [ExternalFact]
    public async Task Stream_ToStream_NonSeekableInput_ToEveryFormat()
    {
        AudioFixture.Input input = fixture.Inputs[7]; // Opus моно, 4 с — самый «неудобный» вход
        Converter converter = new();

        foreach (FileFormat to in Targets)
        {
            await using NonSeekableFile source = new(input.Path);
            using MemoryStream output = new();

            await converter.ConvertAsync(Source.Stream(source), Target.Stream(output, to));

            output.Position = 0;
            Assert.Same(to, await FileTypeDetector.DetectAsync(Source.Stream(output)));
            output.Position = 0;
            double seconds = double.Parse((await fixture.ProbeAsync(output))["duration"], CultureInfo.InvariantCulture);
            Assert.InRange(seconds, input.Seconds - 0.2, input.Seconds + 0.3);
        }
    }

    [ExternalFact]
    public async Task File_ToMemory_GivesBytesAndAReadableStream()
    {
        AudioFixture.Input input = fixture.Inputs[1];

        ConversionResult result = await new Converter().ConvertAsync(Source.File(input.Path), Target.Memory(FileFormat.Wav));

        Assert.NotNull(result.Bytes);
        Assert.NotEmpty(result.Bytes);
        using MemoryStream copy = new();
        await result.Stream!.CopyToAsync(copy);
        Assert.Equal(result.Bytes, copy.ToArray());
    }

    [ExternalFact]
    public async Task AudioOptions_AreVisibleInTheResult()
    {
        AudioFixture.Input input = fixture.Inputs[0];
        string output = Out("параметры.mp3");

        await new Converter().ConvertAsync(
            Source.File(input.Path),
            Target.File(output, FileFormat.Mp3),
            new ConversionOptions { Audio = new AudioOptions { Bitrate = 64_000, SampleRate = 22_050, Channels = 1 } });

        Dictionary<string, string> probe = await fixture.ProbeFileAsync(output);
        Assert.Equal("22050", probe["sample_rate"]);
        Assert.Equal("1", probe["channels"]);
        double bitrate = double.Parse(probe["bit_rate"], CultureInfo.InvariantCulture);
        Assert.InRange(bitrate, 50_000, 80_000);
        Keep(output, "параметры-64к-22кГц-моно");
    }

    [ExternalFact]
    public async Task BrokenFile_IsTypeNotConfirmed_AndNothingIsWritten()
    {
        string output = Out("never.wav");

        ConversionException ex = await Assert.ThrowsAsync<ConversionException>(async () =>
            await new Converter().ConvertAsync(Source.File(fixture.BrokenMp3), Target.File(output, FileFormat.Wav)));

        Assert.Equal(ConversionFailureReason.TypeNotConfirmed, ex.Reason);
        Assert.Contains("broken.mp3", ex.Message);
        Assert.False(File.Exists(output));
    }

    [ExternalFact]
    public async Task FileWithForeignExtension_IsConvertedByItsRealType()
    {
        string output = Out("лжец.wav");

        ConversionResult result = await new Converter().ConvertAsync(Source.File(fixture.LiarOgg), Target.File(output, FileFormat.Wav));

        Assert.Same(FileFormat.Mp3, result.SourceType);
        await AssertDurationAsync(output, 1);
    }

    [ExternalFact]
    public async Task Folder_To_Folder_FortyFiles_ParallelismNeverExceedsTheSetting()
    {
        foreach (int limit in new[] { 1, 4 })
        {
            string target = Out($"пакет-{limit}");
            Converter converter = new(new ConverterSettings { MaxParallelTasks = limit });
            using PeakWatcher watcher = new();

            BatchReport report = await converter.ConvertBatchAsync(Source.Folder(fixture.BatchFolder), Target.Folder(target, FileFormat.Mp3));

            Assert.Equal(40, report.Total);
            Assert.Equal(40, report.Succeeded);
            Assert.Equal(40, Directory.GetFiles(target, "*.mp3").Length);
            Assert.True(watcher.Peak <= limit, $"При лимите {limit} одновременно работало {watcher.Peak} процессов FFmpeg.");
            if (limit == 4)
            {
                Assert.True(watcher.Peak >= 2, $"При лимите 4 параллельности не видно: пик {watcher.Peak}.");
            }
        }
    }

    [ExternalFact]
    public async Task Batch_Cancelled_Midway_LeavesReportNoProcessesAndNoTempFiles()
    {
        string target = Out("отмена");
        Converter converter = new(new ConverterSettings { MaxParallelTasks = 2 });
        using CancellationTokenSource cts = new();
        IProgress<BatchProgress> progress = new Inline(p =>
        {
            if (p.Completed >= 3)
            {
                cts.Cancel();
            }
        });

        BatchReport report = await converter.ConvertBatchAsync(Source.Folder(fixture.BatchFolder), Target.Folder(target, FileFormat.Mp3), progress, cts.Token);

        Assert.Equal(40, report.Total);
        Assert.True(report.Canceled > 0, "Отмена не дошла до записей отчёта.");
        Assert.Equal(40, report.Succeeded + report.Failed + report.Canceled);
        await Task.Delay(1500);
        Assert.Empty(Process.GetProcessesByName("ffmpeg"));
        Assert.Empty(Directory.GetFiles(target, "*.tmp", SearchOption.AllDirectories));
        Assert.Empty(Directory.GetFiles(Path.GetTempPath(), "convert-*.wav"));
    }

    [ExternalFact]
    public async Task Batch_WithBrokenAndLyingFiles_ReportsEachHonestly()
    {
        string target = Out("смесь");
        Source[] sources = [Source.File(fixture.Inputs[0].Path), Source.File(fixture.BrokenMp3), Source.File(fixture.LiarOgg), Source.File(fixture.Inputs[3].Path)];

        BatchReport report = await new Converter().ConvertBatchAsync(sources, Target.Folder(target, FileFormat.Wav));

        Assert.Equal(4, report.Total);
        Assert.Equal(3, report.Succeeded);
        Assert.Equal(1, report.Failed);
        BatchItemResult failed = Assert.Single(report.Items, i => !i.Succeeded);
        Assert.Equal("broken.mp3", failed.SourceName);
        Assert.Equal(ConversionFailureReason.TypeNotConfirmed, failed.Reason);
        Assert.Single(report.FailedSources());
    }

    // Считает, сколько процессов FFmpeg одновременно работает, опрашивая систему каждые несколько миллисекунд.
    private sealed class PeakWatcher : IDisposable
    {
        private readonly CancellationTokenSource _stop = new();
        private readonly Task _loop;
        private int _peak;

        public PeakWatcher()
        {
            _loop = Task.Run(async () =>
            {
                while (!_stop.IsCancellationRequested)
                {
                    Process[] now = Process.GetProcessesByName("ffmpeg");
                    int count = now.Length;
                    foreach (Process process in now)
                    {
                        process.Dispose();
                    }

                    int seen;
                    while (count > (seen = Volatile.Read(ref _peak)))
                    {
                        Interlocked.CompareExchange(ref _peak, count, seen);
                    }

                    await Task.Delay(3);
                }
            });
        }

        public int Peak => Volatile.Read(ref _peak);

        public void Dispose()
        {
            _stop.Cancel();
            _loop.Wait();
            _stop.Dispose();
        }
    }

    private sealed class Inline(Action<BatchProgress> action) : IProgress<BatchProgress>
    {
        public void Report(BatchProgress value)
        {
            action(value);
        }
    }

    // Файл, который читается только вперёд: проверяет работу с потоками без перемотки.
    private sealed class NonSeekableFile(string path) : Stream
    {
        private readonly FileStream _inner = File.OpenRead(path);

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
            return _inner.Read(buffer, offset, count);
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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
