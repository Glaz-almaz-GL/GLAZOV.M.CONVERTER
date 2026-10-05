# GLAZOV.M.CONVERTER

Библиотека .NET для конвертации файлов между форматами — для файлов на диске, папок и потоков (`Stream`).
Перед каждой конвертацией тип входа подтверждается по его начальным байтам: расширение имени доказательством не считается.

- **Звук** — MP3, WAV, OGG (Vorbis; вход Opus принимается), FLAC, M4A (AAC) в любом направлении, включая перекодирование
  с другими параметрами.
- **Изображения** — PNG, JPEG, WebP, BMP, GIF, TIFF, TGA (DDS и PSD — как вход) с поворотом, масштабом и качеством сжатия.
- **Видео** — MP4, MKV, WebM, MOV, AVI между собой и в звук; дорожки переносятся без перекодирования, если можно;
  качество, битрейт, размер кадра, частота кадров, поворот и масштаб.
- **SVG** — в PNG, JPEG, WebP отдельным пакетом `GLAZOV.M.CONVERTER.Svg`; внешние ссылки внутри SVG не загружаются.
- **Документы в PDF** — DOCX, DOC, ODT, RTF, XLSX, XLS, ODS, PPTX, PPT, ODP, HTML, текст и Markdown через LibreOffice
  (устанавливается отдельно); макросы выключены, внешние ссылки документа не загружаются.
- **Картинки в PDF** — PNG, JPEG, WebP, BMP, GIF в PDF одной страницей размером с картинку отдельным пакетом `GLAZOV.M.CONVERTER.Pdf`.
- **Свои форматы** — расширениями, подключаемыми настройкой.

## Быстрый старт

```csharp
using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;

var converter = new Converter();

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

Документ в PDF (нужен LibreOffice) и картинка в PDF (пакет `GLAZOV.M.CONVERTER.Pdf`, `Extensions = { new PdfExtension() }`):

```csharp
await converter.ConvertAsync(Source.File("отчёт.docx"), Target.File("отчёт.pdf", FileFormat.Pdf));
```

Файл `.md` оформляется как Markdown, любое другое имя и поток без имени — простой текст.

Изображение с поворотом, масштабом и качеством:

```csharp
await converter.ConvertAsync(
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
await converter.ConvertAsync(Source.File("лекция.mp4"), Target.File("лекция.mp3", FileFormat.Mp3));

ConversionResult video = await converter.ConvertAsync(Source.File("ролик.mkv"), Target.File("ролик.mp4", FileFormat.Mp4));
Console.WriteLine(video.Reencoded ? "перекодировано" : "перенесено без перекодирования");

await converter.ConvertAsync(
    Source.File("ролик.mp4"), Target.File("ролик.webm", FileFormat.Webm),
    new ConversionOptions
    {
        Video = new VideoOptions { Quality = 70, Width = 1280, FrameRate = 30 },
        Audio = new AudioOptions { Bitrate = 96_000 },
        Operations = { Operation.RotateRight, Operation.ScalePercent(50) },
    });
```

SVG — расширением из пакета `GLAZOV.M.CONVERTER.Svg`:

```csharp
var svg = new Converter(new ConverterSettings { Extensions = { new SvgExtension() } });
await svg.ConvertAsync(Source.File("логотип.svg"), Target.File("логотип.png", FileFormat.Png));
```

Папка целиком, параллельно, с отчётом:

```csharp
BatchReport report = await converter.ConvertBatchAsync(
    Source.Folder("музыка", pattern: "*.ogg", recursive: true),
    Target.Folder("готово", FileFormat.Mp3));

Console.WriteLine($"{report.Succeeded} из {report.Total}; ошибок: {report.Failed}");
var retry = await converter.ConvertBatchAsync(report.FailedSources(), Target.Folder("готово", FileFormat.Mp3));
```

Тип без конвертации и список поддерживаемого:

```csharp
FileFormat type = await FileTypeDetector.DetectAsync(Source.File("загадка.dat"));   // FileFormat.Ogg, … или FileFormat.Unknown
bool ok = converter.Capabilities.CanConvert(FileFormat.Ogg, FileFormat.Mp3);
```

Подробнее — настройки, параметры звука, изображений и видео, SVG, документы в PDF (текст и Markdown, безопасность, скорость),
картинки в PDF, свои расширения, ошибки и их причины — в руководстве
(`GUIDE.md` документации проекта).

## Требования

- .NET 10.
- Для звука, изображений и видео — программа **FFmpeg**, установленная отдельно (библиотека её не содержит); для видео нужен и
  **ffprobe**, он ставится вместе с FFmpeg:
  - Windows: `winget install Gyan.FFmpeg`
  - Linux: `sudo apt install ffmpeg` (или пакет `ffmpeg` вашего дистрибутива)
  - macOS: `brew install ffmpeg`

  Библиотека сама ищет FFmpeg (путь из настроек `ToolPaths`, затем `PATH`, затем привычные места) и, если не нашла,
  говорит, что установить (`converter.Capabilities.Tool("ffmpeg").InstallHint`).
- Для документов в PDF — программа **LibreOffice**, установленная отдельно (библиотека её не содержит):
  - Windows: `winget install TheDocumentFoundation.LibreOffice`
  - Linux: `sudo apt install libreoffice-core libreoffice-writer libreoffice-calc libreoffice-impress` (или пакет вашего дистрибутива)
  - macOS: `brew install --cask libreoffice`

  Поиск тот же: `ToolPaths["soffice"]`, затем `PATH`, привычные места, папка пакетов WinGet;
  подсказка — `converter.Capabilities.Tool("soffice").InstallHint`.
- Для картинок в PDF LibreOffice не нужен: пакет `GLAZOV.M.CONVERTER.Pdf` (подключается ссылкой, `Extensions = { new PdfExtension() }`)
  рисует сам на SkiaSharp, нативные файлы для Windows, Linux и macOS приходят с ним.
- Для SVG FFmpeg не нужен: пакет `GLAZOV.M.CONVERTER.Svg` рисует сам (SkiaSharp и Svg.Skia, нативные файлы
  для Windows, Linux и macOS приходят с ним).

## Лицензия

Apache License 2.0 — файл `LICENSE`; сведения об авторстве, о FFmpeg, LibreOffice и о зависимостях пакетов SVG и PDF — `NOTICE`.
