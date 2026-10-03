# GLAZOV.M.CONVERTER

Библиотека .NET для конвертации файлов между форматами — для файлов на диске, папок и потоков (`Stream`).
Перед каждой конвертацией тип входа подтверждается по его начальным байтам: расширение имени доказательством не считается.

**Первая версия — звук:** MP3, WAV, OGG (Vorbis; вход Opus принимается) в любом направлении, включая перекодирование
с другими параметрами. Изображения, видео в звук и документы в PDF — следующие версии, вызовы будут теми же.

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

Подробнее — настройки, параметры звука, ошибки и их причины — в руководстве (`GUIDE.md` документации проекта).

## Требования

- .NET 10.
- Для звука — программа **FFmpeg**, установленная отдельно (библиотека её не содержит):
  - Windows: `winget install Gyan.FFmpeg`
  - Linux: `sudo apt install ffmpeg` (или пакет `ffmpeg` вашего дистрибутива)
  - macOS: `brew install ffmpeg`

  Библиотека сама ищет FFmpeg (путь из настроек `ToolPaths`, затем `PATH`, затем привычные места) и, если не нашла,
  говорит, что установить (`converter.Capabilities.Tool("ffmpeg").InstallHint`).

## Лицензия

Apache License 2.0 — файл `LICENSE`; сведения об авторстве и о FFmpeg — `NOTICE`.
