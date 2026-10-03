namespace GLAZOV.M.CONVERTER;

/// <summary>Единое сообщение заглушки: контракт принят, реализация следует за ним по дереву проекта.</summary>
internal static class Stub
{
    internal static NotImplementedException NotYet(string what) =>
        new($"{what}: контракт есть, реализации ещё нет (заглушка).");
}
