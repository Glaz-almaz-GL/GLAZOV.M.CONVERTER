namespace GLAZOV.M.CONVERTER.Archives.Safety;

/// <summary>
/// The rules for the names of entries: a name that could leave the folder it is unpacked into is refused, never repaired.
/// </summary>
internal static class EntryNames
{
    /// <summary>
    /// Brings a name to the form with "/" as the separator and without a leading "./", and refuses what is dangerous.
    /// </summary>
    /// <param name="raw">Name as the archive carries it.</param>
    /// <param name="isDirectory">Whether the entry is a folder.</param>
    /// <exception cref="InvalidDataException">The name is empty, absolute, has a drive letter, a parent step ("..") or a zero byte.</exception>
    internal static string Normalize(string raw, bool isDirectory)
    {
        string name = raw.Replace('\\', '/');
        while (name.StartsWith("./", StringComparison.Ordinal))
        {
            name = name[2..];
        }

        if (name.Length == 0 || name == ".")
        {
            throw new InvalidDataException("Опасная или пустая запись: имя записи пусто.");
        }

        if (name.StartsWith('/') || (name.Length >= 2 && name[1] == ':' && char.IsAsciiLetter(name[0])))
        {
            throw new InvalidDataException($"Опасная запись: абсолютный путь «{Shown(name)}» не разрешён.");
        }

        if (name.Contains('\0'))
        {
            throw new InvalidDataException("Опасная запись: в имени есть нулевой символ.");
        }

        if (name.Split('/').Any(part => part == ".."))
        {
            throw new InvalidDataException($"Опасная запись: путь «{Shown(name)}» выходит за пределы папки («..»).");
        }

        return isDirectory && !name.EndsWith('/') ? name + "/" : name;
    }

    private static string Shown(string name)
    {
        return name.Length <= 80 ? name : name[..80] + "…";
    }
}
