namespace L5xploderLib.Models;

public sealed class CustomElementFile : ElementFile
{
    public required string FileExt { get; init; }
    public required string Content { get; init; }
    public string FilePath => BaseFilePath + FileExt;
}