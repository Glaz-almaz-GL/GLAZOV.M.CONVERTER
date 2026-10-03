namespace GLAZOV.M.CONVERTER.Converting;

/// <summary>
/// Параметры качества звука для одного вызова конвертации. Все необязательные:
/// не задано — берётся значение источника или умолчание движка для выбранного формата.
/// </summary>
public sealed class AudioOptions
{
    private int? _bitrate;
    private int? _sampleRate;
    private int? _channels;

    /// <summary>
    /// Битрейт результата, бит/с (например, 192000). Для WAV без сжатия не действует.
    /// Не задан — умолчание движка для формата.
    /// </summary>
    public int? Bitrate
    {
        get => _bitrate;
        set => _bitrate = Positive(value, nameof(Bitrate));
    }

    /// <summary>
    /// Частота дискретизации результата, Гц (например, 44100).
    /// Не задана — частота источника.
    /// </summary>
    public int? SampleRate
    {
        get => _sampleRate;
        set => _sampleRate = Positive(value, nameof(SampleRate));
    }

    /// <summary>
    /// Число каналов результата (1 — моно, 2 — стерео).
    /// Не задано — число каналов источника.
    /// </summary>
    public int? Channels
    {
        get => _channels;
        set => _channels = Positive(value, nameof(Channels));
    }

    private static int? Positive(int? value, string name)
    {
        if (value is <= 0)
        {
            throw new ArgumentOutOfRangeException(name, value, $"Параметр {name} должен быть больше нуля или не задан.");
        }

        return value;
    }
}
