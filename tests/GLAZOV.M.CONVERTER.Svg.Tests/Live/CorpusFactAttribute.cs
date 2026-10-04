namespace GLAZOV.M.CONVERTER.Svg.Tests.Live;

/// <summary>
/// Проверка на чужом наборе настоящих SVG: папку называет переменная окружения <c>CONVERTER_RUN_SVG_CORPUS</c>.
/// Переменная не задана или папки нет — проверка помечается «пропущена» с причиной, а не «пройдена».
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class CorpusFactAttribute : FactAttribute
{
    /// <summary>
    /// Имя переменной окружения с папкой набора.
    /// </summary>
    public const string Variable = "CONVERTER_RUN_SVG_CORPUS";

    public CorpusFactAttribute()
    {
        string? folder = Environment.GetEnvironmentVariable(Variable);
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            Skip = $"Переменная {Variable} не указывает папку с SVG: прогон на чужом наборе не выполнен (не пройден и не провален).";
        }
    }
}
