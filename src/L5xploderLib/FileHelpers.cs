namespace L5xploderLib;

public static class FileHelpers
{
    /// <summary>
    /// Ensures the parent directory for the given file path exists, creating it if necessary.
    /// </summary>
    public static void EnsureDirectoryExists(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
