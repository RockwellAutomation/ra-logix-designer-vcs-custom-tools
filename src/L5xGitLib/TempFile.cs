namespace L5xGitLib;

public sealed class TempFile : IDisposable
{
    public required string Path { get; init; }

    private TempFile()
    {
    }

    public static TempFile FromSuggestedFileName(string fileName)
    {
        string tempPath = System.IO.Path.GetTempPath();
        string baseFileName = System.IO.Path.GetFileNameWithoutExtension(fileName);
        string extension = System.IO.Path.GetExtension(fileName);
        string uniqueFileName = $"{baseFileName}_{Guid.NewGuid()}{extension}";
        string fullPath = System.IO.Path.Combine(tempPath, uniqueFileName);

        var result = new TempFile()
        {
            Path = fullPath
        };

        return result;
    }

    public static TempFile CopyToTempPath(string sourcePath)
    {

        string fileExtension = System.IO.Path.GetExtension(sourcePath);
        string uniqueFileName = $"{System.IO.Path.GetFileNameWithoutExtension(sourcePath)}_{Guid.NewGuid()}{fileExtension}";
        string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), uniqueFileName);

        File.Copy(sourcePath, tempPath, overwrite: true);

        var result = new TempFile()
        {
            Path = tempPath
        };

        return result;
    }

    public static TempFile FromTempFileWithNewExtension(TempFile tempFile, string newExtension)
    {
        var uniqueFileName = System.IO.Path.GetFileNameWithoutExtension(tempFile.Path) + ".L5X";
        string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), uniqueFileName);

        var result = new TempFile()
        {
            Path = tempPath
        };

        return result;
    }


    public void Dispose()
    {
        if (Path is not null && File.Exists(Path))
        {
            File.Delete(Path);
        }
    }
}