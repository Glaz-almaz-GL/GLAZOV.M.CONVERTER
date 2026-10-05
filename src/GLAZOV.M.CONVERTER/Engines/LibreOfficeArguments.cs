namespace GLAZOV.M.CONVERTER.Engines;

/// <summary>
/// Arguments of LibreOffice for converting one document to PDF: a pure function that starts no program.
/// </summary>
internal static class LibreOfficeArguments
{
    /// <summary>
    /// Builds the arguments of a headless run with its own profile.
    /// </summary>
    /// <param name="inputPath">The document to convert.</param>
    /// <param name="outputFolder">Folder for the PDF; the file is named after the input.</param>
    /// <param name="profileFolder">Folder of the user profile of this run only.</param>
    internal static IReadOnlyList<string> ToPdf(string inputPath, string outputFolder, string profileFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputFolder);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileFolder);

        // A shared profile silently loses runs that start together, so every run gets its own.
        string profile = new Uri(Path.GetFullPath(profileFolder)).AbsoluteUri;
        return
        [
            $"-env:UserInstallation={profile}",
            "--headless",
            "--norestore",
            "--nolockcheck",
            "--nologo",
            "--convert-to",
            "pdf",
            "--outdir",
            outputFolder,
            inputPath,
        ];
    }
}
