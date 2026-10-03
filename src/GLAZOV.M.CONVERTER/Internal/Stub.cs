namespace GLAZOV.M.CONVERTER.Internal;

/// <summary>
/// Единое сообщение заглушки: контракт принят, реализация следует за ним по дереву проекта.
/// </summary>
internal static class Stub
{
    internal static NotImplementedException NotYet(string what)
    {
        return new($"{what}: контракт есть, реализации ещё нет (заглушка).");
    }
}
