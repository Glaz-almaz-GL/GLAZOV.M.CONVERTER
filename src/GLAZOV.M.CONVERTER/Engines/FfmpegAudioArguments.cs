using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;

namespace GLAZOV.M.CONVERTER.Engines;

/// <summary>
/// Сборка аргументов FFmpeg для звука: чистая функция без запуска программы.
/// Аргументы — списком, поэтому пробелы и кавычки в путях экранировать не нужно.
/// </summary>
internal static class FfmpegAudioArguments
{
    // Умолчания движка — параметры со значением, не зашитая логика: их перекрывает AudioOptions.
    private const string Mp3DefaultQuality = "2";
    private const string OggDefaultQuality = "5";

    /// <summary>
    /// Собирает аргументы: вход — стандартный ввод, звук без картинок, кодек и параметры по формату результата.
    /// </summary>
    /// <param name="to">Формат результата: MP3, WAV или OGG.</param>
    /// <param name="audio">Параметры звука; задаются в аргументы только заданные.</param>
    /// <param name="outputTarget">Куда писать: <c>pipe:1</c> (стандартный вывод) или путь к временному файлу.</param>
    internal static IReadOnlyList<string> Build(FileFormat to, AudioOptions? audio, string outputTarget)
    {
        ArgumentNullException.ThrowIfNull(to);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputTarget);

        List<string> args = ["-hide_banner", "-loglevel", "error", "-nostats", "-i", "pipe:0", "-vn"];

        if (to.Equals(FileFormat.Mp3))
        {
            args.AddRange(["-c:a", "libmp3lame"]);
            AddQuality(args, audio, Mp3DefaultQuality);
        }
        else if (to.Equals(FileFormat.Ogg))
        {
            args.AddRange(["-c:a", "libvorbis"]);
            AddQuality(args, audio, OggDefaultQuality);
        }
        else if (to.Equals(FileFormat.Wav))
        {
            args.AddRange(["-c:a", "pcm_s16le"]);
        }
        else
        {
            throw new ArgumentException($"Звуковой движок не умеет формат «{to.Name}».", nameof(to));
        }

        if (audio?.SampleRate is int sampleRate)
        {
            args.AddRange(["-ar", sampleRate.ToString(System.Globalization.CultureInfo.InvariantCulture)]);
        }

        if (audio?.Channels is int channels)
        {
            args.AddRange(["-ac", channels.ToString(System.Globalization.CultureInfo.InvariantCulture)]);
        }

        args.AddRange(["-f", to.Extension, "-y", outputTarget]);
        return args;
    }

    private static void AddQuality(List<string> args, AudioOptions? audio, string defaultQuality)
    {
        if (audio?.Bitrate is int bitrate)
        {
            args.AddRange(["-b:a", bitrate.ToString(System.Globalization.CultureInfo.InvariantCulture)]);
        }
        else
        {
            args.AddRange(["-q:a", defaultQuality]);
        }
    }
}
