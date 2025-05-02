namespace L5xploderLib.Models;

public abstract class ElementFile
{
    // Gets the relative file path without the extension, which is determined by the serialization format.
    public required string BaseFilePath { get; init; }
}
