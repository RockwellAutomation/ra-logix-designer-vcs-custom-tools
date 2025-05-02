namespace L5xploderLib.Services;

internal sealed class FilePathRegistry
{
    private readonly HashSet<string> _usedFilePaths = new(StringComparer.OrdinalIgnoreCase);

    public string FindUnreservedFilePath(string folderPath, string baseFileName)
    {
        string fullPath = Path.Combine(folderPath, baseFileName);

        int counter = 1;

        while (_usedFilePaths.Contains(fullPath))
        {
            fullPath = Path.Combine(folderPath, $"{baseFileName}_{counter}");
            counter++;
        }

        _usedFilePaths.Add(fullPath);

        return fullPath;
    }

    public void Reserve(string filePath) => _usedFilePaths.Add(filePath);

    public bool IsReserved(string filePath) => _usedFilePaths.Contains(filePath);
}