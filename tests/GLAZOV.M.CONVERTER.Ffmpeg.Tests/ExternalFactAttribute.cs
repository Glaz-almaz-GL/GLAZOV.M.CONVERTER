using GLAZOV.M.CONVERTER.Ffmpeg.Engines;
using GLAZOV.M.CONVERTER.Ffmpeg.Progress;
using GLAZOV.M.CONVERTER.Engines;
using GLAZOV.M.CONVERTER.Settings;
using GLAZOV.M.CONVERTER.Tooling;

namespace GLAZOV.M.CONVERTER.Ffmpeg.Tests;

/// <summary>
/// Проверка, которой нужна настоящая внешняя программа (FFmpeg). Если её нет на машине — проверка
/// помечается «пропущена» с причиной, а не «пройдена»: пропущенное отличимо от прошедшего.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ExternalFactAttribute : FactAttribute
{
    public ExternalFactAttribute()
    {
        bool found = new ToolLocator(ConverterSettingsSnapshot.Create(new ConverterSettings())).Locate(FfmpegTools.Ffmpeg).Found;
        if (!found)
        {
            Skip = "FFmpeg не найден на этой машине: живая проверка не выполнена (не пройдена и не провалена).";
        }
    }
}
