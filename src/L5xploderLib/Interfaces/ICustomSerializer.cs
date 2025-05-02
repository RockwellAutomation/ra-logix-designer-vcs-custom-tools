using System.Xml.Linq;
using L5xploderLib.Models;

public interface ICustomSerializer
{
    /// <summary>
    /// Serializes select components of the given L5xElementFile to a collection of ElementFiles containing the file path and string content of the file.
    /// The file path is relative to the root document's folder.
    /// </summary>
    IEnumerable<ElementFile> Serialize(XElement element, string elementBaseFile);

    /// <summary>
    /// Rehydrates the parent XElement from the serialized data folder.
    /// </summary>
    IEnumerable<XElement> Deserialize(string folderPath);
}