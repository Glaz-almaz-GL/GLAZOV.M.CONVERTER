# GLAZOV.M.CONVERTER

Библиотека .NET для конвертации файлов между форматами — для файлов на диске, папок и потоков (`Stream`).
Перед каждой конвертацией тип входа подтверждается по его начальным байтам: расширение имени доказательством не считается.

Библиотека состоит из шести пакетов. Ядро не знает ни одного формата и ни одной программы: форматы и движки приносят пакеты,
подключаемые настройкой. `new Converter()` без пакетов не умеет ничего.

| Пакет | Что даёт | Что ещё нужно |
|---|---|---|
| `GLAZOV.M.CONVERTER` | ядро: `Converter`, `Source`, `Target`, очередь, отчёт, механизм расширений | .NET 10 |
| `GLAZOV.M.CONVERTER.Formats` | значения стандартных форматов (`FileFormat.Mp3` … `Text`) и определение типа; приходит зависимостью остальных | — |
| `GLAZOV.M.CONVERTER.Ffmpeg` | **звук** MP3, WAV, OGG, FLAC, M4A; **изображения** PNG, JPEG, WebP, BMP, GIF, TIFF, TGA, AVIF, ICO (DDS и PSD — как вход) с поворотом, масштабом, качеством; **видео** MP4, MKV, WebM, MOV, AVI между собой и в звук (без перекодирования, если можно) | программа FFmpeg |
| `GLAZOV.M.CONVERTER.Office` | **документы в PDF**: DOCX, DOC, ODT, RTF, XLSX, XLS, ODS, PPTX, PPT, ODP, HTML, текст и Markdown; макросы выключены, внешние ссылки не загружаются | программа LibreOffice |
| `GLAZOV.M.CONVERTER.Svg` | **SVG** в PNG, JPEG, WebP; внешние ссылки внутри SVG не загружаются | — (SkiaSharp) |
| `GLAZOV.M.CONVERTER.Pdf` | **картинки в PDF**: PNG, JPEG, WebP, BMP, GIF одной страницей размером с картинку | — (SkiaSharp) |
| `GLAZOV.M.CONVERTER.Archives` | **архив в архив**: ZIP, TAR, TAR.GZ, GZip между собой; записи по одной, защита от опасных имён, шифрования, ссылок и архивов-бомб | — |
| `GLAZOV.M.CONVERTER.ArchivesExtra` | **дополнительные архивы**: 7z, RAR, XZ, TAR.XZ на входе; BZip2 и TAR.BZ2 на входе и выходе; результатом и ZIP, TAR, TAR.GZ, GZip | — (SharpCompress, MIT) |

Все семь вместе дают 245 пар и 45 форматов. **Свои форматы** приносит свой пакет (пример в руководстве).

## Быстрый старт

```csharp
using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Ffmpeg;
using GLAZOV.M.CONVERTER.Formats;      // даёт написание FileFormat.Mp3, FileFormat.Pdf … (C# 14, .NET 10 SDK)
using GLAZOV.M.CONVERTER.Settings;

var converter = new Converter(new ConverterSettings { Extensions = { new FfmpegExtension() } });

// Файл в файл: формат результата задаётся всегда явно
ConversionResult result = await converter.ConvertAsync(
    Source.File("песня.ogg"),
    Target.File("песня.mp3", FileFormat.Mp3));

// Поток в поток
await converter.ConvertAsync(Source.Stream(входнойПоток), Target.Stream(выходнойПоток, FileFormat.Wav));

// Результат в память
ConversionResult inMemory = await converter.ConvertAsync(Source.File("песня.ogg"), Target.Memory(FileFormat.Mp3));
byte[] bytes = inMemory.Bytes!;
```

Какие пакеты подключить — по тому, что нужно; каждый подключается один раз:

```csharp
var all = new Converter(new ConverterSettings
{
    Extensions = { new FfmpegExtension(), new OfficeExtension(), new SvgExtension(), new PdfExtension() },
});
```

Документ в PDF (пакет `.Office` и LibreOffice) и картинка в PDF (пакет `.Pdf`):

```csharp
await all.ConvertAsync(Source.File("отчёт.docx"), Target.File("отчёт.pdf", FileFormat.Pdf));
await all.ConvertAsync(Source.File("логотип.png"), Target.File("логотип.pdf", FileFormat.Pdf));
```

Файл `.md` оформляется как Markdown, любое другое имя и поток без имени — простой текст.

Изображение с поворотом, масштабом и качеством:

```csharp
await all.ConvertAsync(
    Source.File("фото.png"),
    Target.File("фото.jpg", FileFormat.Jpeg),
    new ConversionOptions
    {
        Operations = { Operation.RotateRight, Operation.ScalePercent(50) },
        Image = new ImageOptions { Quality = 85 },
    });
```

Видео: в звук, из контейнера в контейнер (без перекодирования, если кодеки подходят), с параметрами:

```csharp
await all.ConvertAsync(Source.File("лекция.mp4"), Target.File("лекция.mp3", FileFormat.Mp3));

ConversionResult video = await all.ConvertAsync(Source.File("ролик.mkv"), Target.File("ролик.mp4", FileFormat.Mp4));
Console.WriteLine(video.Reencoded ? "перекодировано" : "перенесено без перекодирования");

await all.ConvertAsync(
    Source.File("ролик.mp4"), Target.File("ролик.webm", FileFormat.Webm),
    new ConversionOptions
    {
        Video = new VideoOptions { Quality = 70, Width = 1280, FrameRate = 30 },
        Audio = new AudioOptions { Bitrate = 96_000 },
        Operations = { Operation.RotateRight, Operation.ScalePercent(50) },
    });
```

SVG — пакет `.Svg`:

```csharp
await all.ConvertAsync(Source.File("логотип.svg"), Target.File("логотип.png", FileFormat.Png));
```

Папка целиком, параллельно, с отчётом:

```csharp
BatchReport report = await all.ConvertBatchAsync(
    Source.Folder("музыка", pattern: "*.ogg", recursive: true),
    Target.Folder("готово", FileFormat.Mp3));

Console.WriteLine($"{report.Succeeded} из {report.Total}; ошибок: {report.Failed}");
if (report.Failed > 0)   // пустой список входов отвергается
{
    var retry = await all.ConvertBatchAsync(report.FailedSources(), Target.Folder("готово", FileFormat.Mp3));
}
```

Тип без конвертации и список поддерживаемого:

```csharp
FileFormat type = await all.DetectAsync(Source.File("загадка.dat"));   // FileFormat.Ogg, … или FileFormat.Unknown
bool ok = all.Capabilities.CanConvert(FileFormat.Ogg, FileFormat.Mp3);
IReadOnlyList<FileFormat> known = all.Capabilities.Formats;            // форматы подключённых пакетов
```

Подробнее — какие пакеты ставить, настройки, параметры звука, изображений и видео, SVG, документы в PDF (текст и Markdown,
безопасность, скорость), картинки в PDF, свой пакет форматов, ошибки и их причины — в руководстве (`GUIDE.md` документации проекта).

## Требования

- .NET 10; для написания `FileFormat.Mp3` — .NET 10 SDK (C# 14); без него значения лежат в `AudioFormats.Mp3`, `ImageFormats.Png` и других.
- Для пакета `.Ffmpeg` — программа **FFmpeg**, установленная отдельно (библиотека её не содержит); для видео нужен и
  **ffprobe**, он ставится вместе с FFmpeg:
  - Windows: `winget install Gyan.FFmpeg`
  - Linux: `sudo apt install ffmpeg` (или пакет `ffmpeg` вашего дистрибутива)
  - macOS: `brew install ffmpeg`

  Библиотека сама ищет FFmpeg (путь из настроек `ToolPaths`, затем `PATH`, затем привычные места) и, если не нашла,
  говорит, что установить (`all.Capabilities.Tool("ffmpeg").InstallHint`).
- Для пакета `.Office` — программа **LibreOffice**, установленная отдельно (библиотека её не содержит):
  - Windows: `winget install TheDocumentFoundation.LibreOffice`
  - Linux: `sudo apt install libreoffice-core libreoffice-writer libreoffice-calc libreoffice-impress` (или пакет вашего дистрибутива)
  - macOS: `brew install --cask libreoffice`

  Поиск тот же: `ToolPaths["soffice"]`, затем `PATH`, привычные места, папка пакетов WinGet;
  подсказка — `all.Capabilities.Tool("soffice").InstallHint`.
- Пакетам `.Svg` и `.Pdf` внешние программы не нужны: они рисуют сами на SkiaSharp (и Svg.Skia), нативные файлы для Windows,
  Linux и macOS приходят с ними.

## Лицензия

Apache License 2.0 — файл `LICENSE`; сведения об авторстве, о FFmpeg, LibreOffice и о зависимостях пакетов SVG и PDF — `NOTICE`.
