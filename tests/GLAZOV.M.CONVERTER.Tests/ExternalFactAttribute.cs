using GLAZOV.M.CONVERTER.Settings;
using GLAZOV.M.CONVERTER.Tooling;

namespace GLAZOV.M.CONVERTER.Tests;

/// <summary>
/// Проверка, которой нужна настоящая внешняя программа (FFmpeg). Если её нет на машине — проверка
/// помечается «пропущена» с причиной, а не «пройдена»: пропущенное отличимо от прошедшего.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ExternalFactAttribute : FactAttribute
{
    public ExternalFactAttribute()
    {
        bool found = new ToolLocator(ConverterSettingsSnapshot.Create(new ConverterSettings())).Locate("ffmpeg").Found;
        if (!found)
        {
            Skip = "FFmpeg не найден на этой машине: живая проверка не выполнена (не пройдена и не провалена).";
        }
    }
}
