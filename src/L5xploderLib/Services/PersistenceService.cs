using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using L5xploderLib.Interfaces;
using L5xploderLib.Models;

namespace L5xploderLib.Services;

/// <summary>
/// Handles saving an exploded L5x representation to files on disk.
/// </summary>
internal abstract class PersistenceService : IPersistenceService
{
    public required L5xSerializationOptions SerializationOptions { get; init; }

    public required string ExplodedDir { get; init; }

    // Use a fixed subdir for output because we have to clean the directory when saving
    // We don't want to just delete all the content of the user specified directory directly
    // because the user may not expect that.
    public string ExplodedSubDir => Paths.GetExplodedSubDir(ExplodedDir);

    protected abstract string FileExtension { get; }

    public string RootDocumentPath => Path.Combine(ExplodedSubDir, Constants.RootDocumentBaseFileName + FileExtension);

    private string OptionsFilePath => Paths.GetOptionsFilePath(ExplodedDir);

    public void Save(XDocument rootDoc, IEnumerable<ElementFile> elementFiles)
    {
        VerifyNoDuplicateElementFiles(elementFiles, FileExtension);
        InitializeExplodedSubDir();
        SaveElements(elementFiles, ExplodedSubDir);
        SaveRootImpl(rootDoc, RootDocumentPath);
        SerializationOptions.Save(OptionsFilePath);
    }

    public XDocument LoadRoot()
    {
        return LoadRootImpl(RootDocumentPath);
    }

    public XElement LoadElement(string relativeFilePathWithoutExtension)
    {
        var absoluteFilePath = GetAbsoluteFilePath(relativeFilePathWithoutExtension);
        return LoadElementImpl(absoluteFilePath);
    }

    public IEnumerable<XElement> LoadCustomSerializedElements(string relativeFolderPath, IEnumerable<ICustomSerializer>? serializers)
    {
        var absoluteFolderPath = GetAbsoluteFolderPath(relativeFolderPath);

        if (serializers is null)
        {
            return Enumerable.Empty<XElement>();
        }

        var results = new List<XElement>();
        foreach (var serializer in serializers)
        {
            results.AddRange(serializer.Deserialize(absoluteFolderPath));
        }

        return results;
    }

    /// <summary>
    /// Reads and deserializes the given file from the chosen serialization format to an XElement.
    /// </summary>
    protected abstract XElement LoadElementImpl(string absoluteFilePath);

    /// <summary>
    /// Reads and deserializes the given file from the chosen serialization format to an XDocument.
    /// </summary>
    protected abstract XDocument LoadRootImpl(string absoluteFilePath);

    /// <summary>
    /// Serializes and saves a represenation of the XElement to the specified file path in the chosen serialization format.
    /// Implementations of this method must be thread-safe.
    /// </summary>
    /// <param name="element">The XML element to save.</param>
    /// <param name="absoluteFilePath">The absolute file path where the element should be saved.</param>
    protected abstract void SaveElementImpl(XElement element, string absoluteFilePath);

    /// <summary>
    /// Serializes and saves a represenation of the XDocument to the specified file path in the chosen serialization format.
    /// Implementations of this method must be thread-safe.
    /// </summary>
    /// <param name="element">The XML element to save.</param>
    /// <param name="absoluteFilePath">The absolute file path where the element should be saved.</param>
    protected abstract void SaveRootImpl(XDocument xmlDoc, string absoluteFilePath);


    private void InitializeExplodedSubDir()
    {
        if (Directory.Exists(ExplodedSubDir))
        {
            // Directory.Delete has been problematic with I/O exceptions / file in use
            RetryHandler.RetryOnException(
                () => Directory.Delete(ExplodedSubDir, true),
                maxRetries: 10,
                delayMilliseconds: 250,
                typeof(IOException));
        }

        Directory.CreateDirectory(ExplodedSubDir);
    }

    private void SaveElements(
        IEnumerable<ElementFile> elementFiles,
        string parentDir)
    {
        // Create the empty directory structure first.  Then write all xml files to disk.

        // Windows does not have any way that I'm aware of to quickly create a large
        // empty directory structure.  If we just do them all in parallel, there is a 
        // risk of I/O errors because creating a directory creates the necessary parent
        // directories.  The contention (if done in parallel) is on the creation of the parents.

        // Because of this, we'll create multiple ForEach-Parallel loops, one for each
        // directory level, which avoids this contention.
        var directories = elementFiles
            .Select(file =>
            {
                switch (file)
                {
                    case L5xElementFile l5xFile:
                        return Path.GetDirectoryName(l5xFile.BaseFilePath) ?? string.Empty;
                    case CustomElementFile customFile:
                        return Path.GetDirectoryName(customFile.FilePath) ?? string.Empty;
                    default:
                        throw new InvalidOperationException($"Unsupported element file type: {file.GetType().Name}");
                }
            })
            .Distinct(StringComparer.OrdinalIgnoreCase);

        var maxDepth = directories
            .Select(dir => dir.Count(c => Path.DirectorySeparatorChar == c))
            .Max();

        Directory.CreateDirectory(parentDir);

        for (int depth = 0; depth <= maxDepth; depth++)
        {
            var dirsAtLevel = directories
                .Where(dir => dir.Count(c => Path.DirectorySeparatorChar == c) == depth)
                .Select(dir => Path.Combine(parentDir, dir))
                .ToList();

            Parallel.ForEach(dirsAtLevel, directory => Directory.CreateDirectory(directory));
        }

        // Now save the files in parallel
        Parallel.ForEach(elementFiles, SaveElement);
    }

    private void SaveElement(ElementFile elementFile)
    {
        switch (elementFile)
        {
            case L5xElementFile l5xFile:
                SaveL5xElement(l5xFile);
                break;
            case CustomElementFile customFile:
                SaveCustomElement(customFile);
                break;
            default:
                throw new InvalidOperationException($"Unsupported element file type: {elementFile.GetType().Name}");
        }
    }

    private void SaveL5xElement(L5xElementFile elementFile)
    {
        var absoluteFilePath = GetAbsoluteFilePath(elementFile.BaseFilePath);
        SaveElementImpl(elementFile.Element, absoluteFilePath);
    }

    private void SaveCustomElement(CustomElementFile customFile)
    {
        var absoluteFilePath = Path.Combine(ExplodedSubDir, customFile.FilePath);
        File.WriteAllText(absoluteFilePath, customFile.Content);
    }

    public bool DirectoryExists(string relativeFolderPath)
    {
        return Directory.Exists(Path.Combine(ExplodedSubDir, relativeFolderPath));
    }

    public IEnumerable<string> GetDirectories(string relativeFolderPath)
    {
        var absolutePath = Path.Combine(ExplodedSubDir, relativeFolderPath);
        return Directory.GetDirectories(absolutePath)
            .Select(dir => Path.GetRelativePath(ExplodedSubDir, dir))
            .Where(dir => !string.IsNullOrEmpty(dir));
    }

    public IEnumerable<string> GetBaseFiles(string relativeFolderPath)
    {
        var absolutePath = Path.Combine(ExplodedSubDir, relativeFolderPath);
        return Directory.GetFiles(absolutePath, "*" + FileExtension)
            .Select(file => Path.GetFileNameWithoutExtension(file))
            .Where(file => !string.IsNullOrEmpty(file));
    }

    protected string GetAbsoluteFilePath(string relativeFilePathWithoutExtension)
    {
        return Path.Combine(ExplodedSubDir, relativeFilePathWithoutExtension + FileExtension);
    }

    protected string GetAbsoluteFolderPath(string relativeFolderPath)
    {
        return Path.Combine(ExplodedSubDir, relativeFolderPath);

    }

    private static void VerifyNoDuplicateElementFiles(IEnumerable<ElementFile> elementFiles, string defaultFileExt)
    {
        var elementFileNames = elementFiles
            .OfType<L5xElementFile>()
            .Select(file => file.BaseFilePath + defaultFileExt);

        var customSerializedFileNames = elementFiles
            .OfType<CustomElementFile>()
            .Select(file => file.FilePath);

        var duplicates = elementFileNames
            .Concat(customSerializedFileNames)
            .GroupBy(fileName => fileName, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);

        if (duplicates.Any())
        {
            throw new InvalidDataException($"Duplicate file paths found: {string.Join(", ", duplicates)}");
        }
    }
}
