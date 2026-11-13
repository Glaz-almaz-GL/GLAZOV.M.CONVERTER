namespace GLAZOV.M.CONVERTER.Converting;

/// <summary>
/// Параметры качества звука для одного вызова конвертации. Все необязательные:
/// не задано — берётся значение источника или умолчание движка для выбранного формата.
/// </summary>
public sealed class AudioOptions
{
    /// <summary>
    /// Битрейт результата, бит/с (например, 192000). Для WAV без сжатия не действует.
    /// Не задан — умолчание движка для формата.
    /// </summary>
    public int? Bitrate
    {
        get;
        set => field = CheckPositive(value, nameof(Bitrate));
    }

    /// <summary>
    /// Частота дискретизации результата, Гц (например, 44100).
    /// Не задана — частота источника.
    /// </summary>
    public int? SampleRate
    {
        get;
        set => field = CheckPositive(value, nameof(SampleRate));
    }

    /// <summary>
    /// Число каналов результата (1 — моно, 2 — стерео).
    /// Не задано — число каналов источника.
    /// </summary>
    public int? Channels
    {
        get;
        set => field = CheckPositive(value, nameof(Channels));
    }

    private static int? CheckPositive(int? value, string name)
    {
        return value is <= 0
            ? throw new ArgumentOutOfRangeException(name, value, $"Параметр {name} должен быть больше нуля или не задан.")
            : value;
    }
}
