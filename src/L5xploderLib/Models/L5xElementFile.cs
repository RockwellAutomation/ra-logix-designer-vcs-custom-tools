using System.Xml.Linq;

namespace L5xploderLib.Models;

public sealed class L5xElementFile : ElementFile
{
    // The element to serialize
    public required XElement Element { get; init; }
}