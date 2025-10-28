using System.Xml.Linq;
using L5xploderLib.Models;

namespace L5xploderLib.Interfaces;

/// <summary>
/// Interface for persistence services which handle saving/importing the exploded L5x representation to/from disk.
/// </summary>
public interface IPersistenceService
{
    L5xSerializationOptions SerializationOptions { get; }

    string ExplodedSubDir { get; }

    string RootDocumentPath { get; }

    void Save(XDocument rootDoc, IEnumerable<ElementFile> elementFiles);

    XElement LoadElement(string relativeFilePathWithoutExtension);

    IEnumerable<XElement> LoadCustomSerializedElements(string relativeFolderPath, IEnumerable<ICustomSerializer>? serializers);

    XDocument LoadRoot();

    bool DirectoryExists(string relativeFolderPath);

    IEnumerable<string> GetDirectories(string relativeFolderPath);

    IEnumerable<string> GetBaseFiles(string relativeFolderPath);

    Task WarmReadCacheAsync();
}