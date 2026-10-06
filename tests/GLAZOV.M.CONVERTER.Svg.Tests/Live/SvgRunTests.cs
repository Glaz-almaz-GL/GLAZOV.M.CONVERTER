using System.Diagnostics;
using GLAZOV.M.CONVERTER.Batch;
using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Errors;
using GLAZOV.M.CONVERTER.Formats;
using GLAZOV.M.CONVERTER.Settings;
using SkiaSharp;
using Xunit.Abstractions;

namespace GLAZOV.M.CONVERTER.Svg.Tests.Live;

/// <summary>
/// Прогон SVG на настоящих файлах через публичные вызовы: иконка в духе Inkscape, диаграмма со стилями CSS и маркерами,
/// документ A4 в миллиметрах с текстом и градиентами — во все три формата; поворот и масштаб; поток без перемотки;
/// папка параллельно с повреждённым файлом внутри; внешняя ссылка; по желанию — чужой набор SVG из папки
/// <c>CONVERTER_RUN_SVG_CORPUS</c>. Результаты читаются обратно декодером Skia. Если переменная CONVERTER_RUN_KEEP
/// указывает папку — результаты сохраняются туда для просмотра.
/// </summary>
public sealed class SvgRunTests(ITestOutputHelper output) : IDisposable
{
    /// <summary>Вход прогона: образец и ожидаемый размер результата.</summary>
    private sealed record Input(string Name, int Width, int Height, double MinPainted);

    private static readonly Input[] Inputs =
    [
        new("run-icon.svg", 128, 128, 0.6), // круг, обрезанный по радиусу 15,5 из 16,9: около 66 %
        new("run-diagram.svg", 640, 300, 0.99),
        new("run-document.svg", 794, 1123, 0.99),
    ];

    private static readonly FileFormat[] Targets = [FileFormat.Png, FileFormat.Jpeg, FileFormat.Webp];

    private readonly string _work = Path.Combine(Path.GetTempPath(), "convrun-svg-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_work))
        {
            Directory.Delete(_work, recursive: true);
        }
    }

    private static string Sample(string name)
    {
        return Path.Combine(AppContext.BaseDirectory, "Samples", name);
    }

    private string Out(string name)
    {
        Directory.CreateDirectory(_work);
        return Path.Combine(_work, name);
    }

    private static Converter Svg(int parallel = 4)
    {
        ConverterSettings settings = new() { MaxParallelTasks = parallel };
        settings.Extensions.Add(new SvgExtension());
        return new Converter(settings);
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

    private static SKBitmap Decode(string path)
    {
        return SKBitmap.Decode(path) ?? throw new InvalidDataException($"Результат «{path}» не декодируется.");
    }

    // Доля непрозрачных пикселей: пустой рисунок означал бы, что документ не нарисован.
    private static double Painted(SKBitmap bitmap)
    {
        long painted = 0;
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y).Alpha > 0)
                {
                    painted++;
                }
            }
        }

        return (double)painted / ((long)bitmap.Width * bitmap.Height);
    }

    [Fact]
    public async Task EveryRealSvg_ToEveryFormat_FileToFile()
    {
        Converter converter = Svg();

        foreach (Input input in Inputs)
        {
            foreach (FileFormat to in Targets)
            {
                string label = "svg-" + Path.GetFileNameWithoutExtension(input.Name)[4..] + "-в-" + to.Name;
                string produced = Out($"{label}.{to.Extension}");

                ConversionResult result = await converter.ConvertAsync(Source.File(Sample(input.Name)), Target.File(produced, to));

                Assert.True(result.Succeeded);
                Assert.Equal(FileFormat.Svg, result.SourceType);
                Assert.Equal(produced, result.OutputPath);
                using SKBitmap bitmap = Decode(produced);
                Assert.Equal((input.Width, input.Height), (bitmap.Width, bitmap.Height));
                Assert.True(Painted(bitmap) >= input.MinPainted, $"{label}: нарисовано {Painted(bitmap):P0}.");
                Keep(produced, label);
            }
        }
    }

    [Fact]
    public async Task Document_TextAndGradients_AreDrawn_InTheRightPlaces()
    {
        string produced = Out("документ.png");

        await Svg().ConvertAsync(Source.File(Sample("run-document.svg")), Target.File(produced, FileFormat.Png));

        using SKBitmap page = Decode(produced);
        SKColor headerLeft = page.GetPixel(5, 5);
        SKColor headerRight = page.GetPixel(page.Width - 120, 140);
        Assert.True(headerLeft.Blue > headerLeft.Red && headerLeft.Red < 60, $"Начало градиента заголовка: {headerLeft}.");
        Assert.True(headerRight.Blue > 180, $"Конец градиента заголовка: {headerRight}.");

        // Строки абзаца: между 55 и 80 мм по высоте должны быть тёмные пиксели текста.
        int dark = 0;
        for (int y = (int)(52 * 3.78); y < (int)(80 * 3.78); y++)
        {
            for (int x = (int)(15 * 3.78); x < (int)(190 * 3.78); x++)
            {
                SKColor c = page.GetPixel(x, y);
                if (c.Red + c.Green + c.Blue < 300)
                {
                    dark++;
                }
            }
        }

        Assert.True(dark > 1000, $"Текст абзаца не нарисован: {dark} тёмных пикселей.");
    }

    [Fact]
    public async Task RotateAndScale_OnRealFiles_AreRedrawn()
    {
        Converter converter = Svg();
        string rotated = Out("svg-diagram-поворот-масштаб-150.png");
        string half = Out("svg-document-масштаб-50.webp");
        string icon = Out("svg-icon-масштаб-400.jpg");

        await converter.ConvertAsync(
            Source.File(Sample("run-diagram.svg")),
            Target.File(rotated, FileFormat.Png),
            new ConversionOptions { Operations = { Operation.RotateRight, Operation.ScalePercent(150) } });
        await converter.ConvertAsync(
            Source.File(Sample("run-document.svg")),
            Target.File(half, FileFormat.Webp),
            new ConversionOptions { Operations = { Operation.ScalePercent(50) }, Image = new ImageOptions { Quality = 90 } });
        await converter.ConvertAsync(
            Source.File(Sample("run-icon.svg")),
            Target.File(icon, FileFormat.Jpeg),
            new ConversionOptions { Operations = { Operation.ScalePercent(400) }, Image = new ImageOptions { Quality = 95 } });

        using SKBitmap r = Decode(rotated);
        using SKBitmap h = Decode(half);
        using SKBitmap i = Decode(icon);
        Assert.Equal((450, 960), (r.Width, r.Height));
        Assert.Equal((397, 561), (h.Width, h.Height));
        Assert.Equal((512, 512), (i.Width, i.Height));
        Keep(rotated, Path.GetFileNameWithoutExtension(rotated));
        Keep(half, Path.GetFileNameWithoutExtension(half));
        Keep(icon, Path.GetFileNameWithoutExtension(icon));
    }

    [Fact]
    public async Task ForwardOnlyStream_ToStream_EqualsFileToFile()
    {
        Converter converter = Svg();
        string fromFile = Out("из-файла.png");
        await converter.ConvertAsync(Source.File(Sample("run-diagram.svg")), Target.File(fromFile, FileFormat.Png));

        await using ForwardOnlyStream input = new(Sample("run-diagram.svg"));
        using MemoryStream result = new();
        await converter.ConvertAsync(Source.Stream(input), Target.Stream(result, FileFormat.Png));

        Assert.Equal(await File.ReadAllBytesAsync(fromFile), result.ToArray());
    }

    [Fact]
    public async Task Folder_ThirtyFilesAndOneBroken_Parallel_ReportNamesTheBrokenOne()
    {
        string folder = Out("пакет");
        Directory.CreateDirectory(folder);
        for (int n = 0; n < 10; n++)
        {
            foreach (Input input in Inputs)
            {
                File.Copy(Sample(input.Name), Path.Combine(folder, $"{n:00}-{input.Name}"));
            }
        }

        string document = await File.ReadAllTextAsync(Sample("run-document.svg"));
        await File.WriteAllTextAsync(Path.Combine(folder, "обрезанный.svg"), document[..(document.Length / 2)]);
        string target = Out("пакет-результат");

        Stopwatch watch = Stopwatch.StartNew();
        BatchReport report = await Svg(parallel: 4).ConvertBatchAsync(Source.Folder(folder), Target.Folder(target, FileFormat.Webp));
        watch.Stop();
        output.WriteLine($"Пакет: {report.Total} входов, {report.Succeeded} готово, {report.Failed} ошибок, {watch.Elapsed.TotalSeconds:0.00} с.");

        Assert.Equal(31, report.Total);
        Assert.Equal(30, report.Succeeded);
        BatchItemResult broken = Assert.Single(report.Items, i => !i.Succeeded);
        Assert.Equal("обрезанный.svg", broken.SourceName);
        Assert.Equal(ConversionFailureReason.EngineFailed, broken.Reason);
        Assert.Contains("повреждён", broken.Message);
        Assert.Equal(30, Directory.GetFiles(target, "*.webp").Length);
        Keep(Directory.GetFiles(target, "*.webp").Order().First(), "svg-пакет-первый");
    }

    [Fact]
    public async Task RealDocument_WithLinkToANeighbourFile_DoesNotLoadIt()
    {
        string folder = Out("ссылка");
        Directory.CreateDirectory(folder);
        string neighbour = Path.Combine(folder, "соседний.png");
        await Svg().ConvertAsync(Source.File(Sample("run-icon.svg")), Target.File(neighbour, FileFormat.Png));
        string svg = Path.Combine(folder, "со-ссылкой.svg");
        await File.WriteAllTextAsync(svg, $"""
            <svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="300" height="150">
              <rect width="300" height="150" fill="#ffffff"/>
              <image x="10" y="10" width="128" height="128" xlink:href="соседний.png"/>
              <image x="160" y="10" width="128" height="128" xlink:href="{new Uri(neighbour).AbsoluteUri}"/>
              <text x="150" y="145" font-size="10" text-anchor="middle">ссылки не загружаются</text>
            </svg>
            """);
        string produced = Out("svg-внешняя-ссылка.png");

        await Svg().ConvertAsync(Source.File(svg), Target.File(produced, FileFormat.Png));

        using SKBitmap bitmap = Decode(produced);
        Assert.Equal(SKColors.White, bitmap.GetPixel(74, 74));
        Assert.Equal(SKColors.White, bitmap.GetPixel(224, 74));
        Keep(produced, "svg-внешняя-ссылка");
    }

    [CorpusFact]
    public async Task ForeignCorpus_EveryFile_ConvertsOrFailsCleanly_WithinTimeLimit()
    {
        string corpus = Environment.GetEnvironmentVariable(CorpusFactAttribute.Variable)!;
        string[] files = Directory.GetFiles(corpus, "*.svg", SearchOption.AllDirectories);
        Converter converter = Svg();
        Dictionary<string, int> outcomes = [];
        List<string> unexpected = [];
        string target = Out("набор");
        Directory.CreateDirectory(target);
        Stopwatch total = Stopwatch.StartNew();

        for (int n = 0; n < files.Length; n++)
        {
            string produced = Path.Combine(target, $"{n:0000}-{Path.GetFileNameWithoutExtension(files[n])}.png");
            Task<ConversionResult> run = converter.ConvertAsync(Source.File(files[n]), Target.File(produced, FileFormat.Png));
            if (await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(30))) != run)
            {
                unexpected.Add($"{files[n]}: дольше 30 с");
                continue;
            }

            string outcome;
            try
            {
                await run;
                outcome = "готово";
                if (n < 80)
                {
                    Keep(produced, "svg-набор-" + Path.GetFileNameWithoutExtension(produced));
                }
            }
            catch (ConversionException ex)
            {
                outcome = ex.Reason.ToString();
                output.WriteLine($"{ex.Reason}: {files[n]} — {ex.Message}");
            }
            catch (Exception ex)
            {
                outcome = "непредвиденно";
                unexpected.Add($"{files[n]}: {ex.GetType().Name}: {ex.Message}");
            }

            outcomes[outcome] = outcomes.GetValueOrDefault(outcome) + 1;
        }

        total.Stop();
        output.WriteLine($"Набор: {files.Length} файлов за {total.Elapsed.TotalSeconds:0.0} с — "
            + string.Join(", ", outcomes.Select(o => $"{o.Key} {o.Value}")));
        foreach (string line in unexpected)
        {
            output.WriteLine("НЕПРЕДВИДЕННО " + line);
        }

        Assert.NotEmpty(files);
        Assert.Empty(unexpected);
    }
}
