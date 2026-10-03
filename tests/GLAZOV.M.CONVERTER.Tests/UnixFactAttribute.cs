namespace GLAZOV.M.CONVERTER.Tests;

/// <summary>
/// Проверка, имеющая смысл только на Linux и macOS (права доступа к файлам). На Windows помечается «пропущена».
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class UnixFactAttribute : FactAttribute
{
    public UnixFactAttribute()
    {
        if (OperatingSystem.IsWindows())
        {
            Skip = "Проверка прав на выполнение имеет смысл только на Linux и macOS.";
        }
    }
}
