using GLAZOV.M.CONVERTER.Engines;
using GLAZOV.M.CONVERTER.Settings;
using GLAZOV.M.CONVERTER.Tooling;

namespace GLAZOV.M.CONVERTER.Tests;

/// <summary>
/// A check that needs the real LibreOffice. Without it on the machine the check is skipped with the reason,
/// not passed: a skipped check is told apart from a passed one.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class OfficeFactAttribute : FactAttribute
{
    public OfficeFactAttribute()
    {
        bool found = new ToolLocator(ConverterSettingsSnapshot.Create(new ConverterSettings())).Locate(LibreOfficeTools.Soffice).Found;
        if (!found)
        {
            Skip = "LibreOffice не найден на этой машине: живая проверка не выполнена (не пройдена и не провалена).";
        }
    }
}
