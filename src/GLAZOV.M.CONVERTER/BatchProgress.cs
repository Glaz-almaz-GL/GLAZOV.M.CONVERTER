namespace GLAZOV.M.CONVERTER;

/// <summary>Ход пакетной конвертации.</summary>
public sealed class BatchProgress
{
    internal BatchProgress(int completed, int total, string currentName)
    {
        Completed = completed;
        Total = total;
        CurrentName = currentName;
    }

    /// <summary>Сколько входов уже обработано.</summary>
    public int Completed { get; }

    /// <summary>Сколько входов всего.</summary>
    public int Total { get; }

    /// <summary>Имя входа, обрабатываемого сейчас.</summary>
    public string CurrentName { get; }
}
