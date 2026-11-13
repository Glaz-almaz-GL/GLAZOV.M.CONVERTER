namespace GLAZOV.M.CONVERTER.Office.Engines;

/// <summary>
/// Removal of the work folder of a LibreOffice run.
/// </summary>
internal static class WorkFolder
{
    /// <summary>
    /// The program may still hold the profile for a moment after it was killed, so removal is retried a few times.
    /// </summary>
    /// <param name="folder">The folder to remove.</param>
    internal static async Task RemoveAsync(string folder)
    {
        for (int attempt = 0; attempt < 10 && Directory.Exists(folder); attempt++)
        {
            try
            {
                Directory.Delete(folder, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                await Task.Delay(100).ConfigureAwait(false);
            }
        }
    }
}
