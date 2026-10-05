using GLAZOV.M.CONVERTER.Engines;
using GLAZOV.M.CONVERTER.Settings;
using GLAZOV.M.CONVERTER.Tooling;

namespace GLAZOV.M.CONVERTER.Tests;

/// <summary>
/// A parameterised check that needs the real LibreOffice; skipped with the reason when it is not on the machine.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class OfficeTheoryAttribute : TheoryAttribute
{
    public OfficeTheoryAttribute()
    {
        bool found = new ToolLocator(ConverterSettingsSnapshot.Create(new ConverterSettings())).Locate(LibreOfficeTools.Soffice).Found;
        if (!found)
        {
            Skip = "LibreOffice не найден на этой машине: живая проверка не выполнена (не пройдена и не провалена).";
        }
    }
}
