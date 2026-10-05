using GLAZOV.M.CONVERTER.Engines;
using GLAZOV.M.CONVERTER.Settings;
using GLAZOV.M.CONVERTER.Tooling;

namespace GLAZOV.M.CONVERTER.Tests;

/// <summary>
/// Параметризованная проверка, которой нужна настоящая внешняя программа (FFmpeg). Нет программы — «пропущена» с причиной.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ExternalTheoryAttribute : TheoryAttribute
{
    public ExternalTheoryAttribute()
    {
        bool found = new ToolLocator(ConverterSettingsSnapshot.Create(new ConverterSettings())).Locate(FfmpegTools.Ffmpeg).Found;
        if (!found)
        {
            Skip = "FFmpeg не найден на этой машине: живая проверка не выполнена (не пройдена и не провалена).";
        }
    }
}
