using System.Xml.Linq;
using L5xploderLib.Interfaces;

namespace L5xploderLib;

public sealed class L5xExploderConfig
{
    /// <summary>
    /// XPath expression to select elements from the L5x document for this configuration.
    /// </summary>
    public required string XPath { get; init; }

    /// <summary>
    /// The folder in which to store this element's exploded representation, relative to the exploded root document's containing folder.
    /// </summary>
    public required Func<XElement, string> FolderGenerator { get; init; }

    /// <summary>
    /// Function to generate the base file name (without extension) for this element.
    /// </summary>
    public required Func<XElement, string> BaseFileNameGenerator { get; init; }

    /// <summary>
    /// Optional function to sort child elements in the rehydrated L5x file.
    /// May be required to ensure dependencies are imported before their dependents.
    /// </summary>
    public Func<IEnumerable<XElement>, IList<XElement>>? SortFunction { get; init; }

    /// <summary>
    /// Optional static serializer for persisting child elements in a format other than the selected default serialization format.
    /// A use case might be persisting Structured Text (ST) as standalone text files instead of CDATA embedded in XML.
    /// </summary>
    public IEnumerable<ICustomSerializer>? CustomSerializers { get; init; }

    /// <summary>
    /// Optional tranformation of element representation before persisting to disk
    /// </summary>
    public IEnumerable<IXElementTransformer>? Transformers { get; init; }

    /// <summary>
    /// Optional child configurations for further breaking apart of nested elements.
    /// </summary>
    public IEnumerable<L5xExploderConfig>? ChildConfigs { get; init; }
}