# GLAZOV.M.CONVERTER

Библиотека .NET для конвертации файлов между форматами — для файлов на диске, папок и потоков (`Stream`).
Перед каждой конвертацией тип входа подтверждается по его начальным байтам: расширение имени доказательством не считается.

- **Звук** — MP3, WAV, OGG (Vorbis; вход Opus принимается), FLAC, M4A (AAC) в любом направлении, включая перекодирование
  с другими параметрами.
- **Изображения** — PNG, JPEG, WebP, BMP, GIF, TIFF, TGA (DDS и PSD — как вход) с поворотом, масштабом и качеством сжатия.
- **SVG** — в PNG, JPEG, WebP отдельным пакетом `GLAZOV.M.CONVERTER.Svg`; внешние ссылки внутри SVG не загружаются.
- **Свои форматы** — расширениями, подключаемыми настройкой.

Видео в звук и документы в PDF — следующие версии, вызовы будут теми же.

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

Подробнее — настройки, параметры звука и изображений, SVG, свои расширения, ошибки и их причины — в руководстве
(`GUIDE.md` документации проекта).

## Требования

- .NET 10.
- Для звука и изображений — программа **FFmpeg**, установленная отдельно (библиотека её не содержит):
  - Windows: `winget install Gyan.FFmpeg`
  - Linux: `sudo apt install ffmpeg` (или пакет `ffmpeg` вашего дистрибутива)
  - macOS: `brew install ffmpeg`

  Библиотека сама ищет FFmpeg (путь из настроек `ToolPaths`, затем `PATH`, затем привычные места) и, если не нашла,
  говорит, что установить (`converter.Capabilities.Tool("ffmpeg").InstallHint`).
- Для SVG FFmpeg не нужен: пакет `GLAZOV.M.CONVERTER.Svg` рисует сам (SkiaSharp и Svg.Skia, нативные файлы
  для Windows, Linux и macOS приходят с ним).

## Лицензия

Apache License 2.0 — файл `LICENSE`; сведения об авторстве, о FFmpeg и о зависимостях пакета SVG — `NOTICE`.
