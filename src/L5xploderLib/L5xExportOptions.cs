using System.Xml.Linq;

namespace L5xploderLib;

/// <summary>
/// Parses and provides access to the ExportOptions attribute from an L5X root element.
/// The ExportOptions attribute is a space-delimited list of export flags set by Logix Designer
/// during L5X export, such as "NoRawData L5KData DecoratedData Dependencies ForceProtectedEncoding AllProjDocTrans".
/// </summary>
public sealed class L5xExportOptions
{
    private readonly HashSet<string> _options;

    /// <summary>
    /// Creates an L5xExportOptions from the space-delimited ExportOptions attribute value.
    /// </summary>
    /// <param name="exportOptionsValue">The raw ExportOptions attribute value (space-delimited).</param>
    public L5xExportOptions(string? exportOptionsValue)
    {
        _options = string.IsNullOrWhiteSpace(exportOptionsValue)
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(exportOptionsValue.Split(' ', StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Parses the ExportOptions from the root element of an L5X document.
    /// </summary>
    public static L5xExportOptions FromRootElement(XElement rootElement)
    {
        var exportOptionsValue = rootElement.Attribute("ExportOptions")?.Value;
        return new L5xExportOptions(exportOptionsValue);
    }

    /// <summary>
    /// Returns true if the specified option is present in the ExportOptions.
    /// Comparison is case-insensitive.
    /// </summary>
    public bool HasOption(string option) => _options.Contains(option);

    /// <summary>
    /// Returns true if the L5X was exported with the "Dependencies" option,
    /// which causes Logix Designer to include explicit &lt;Dependencies&gt; elements
    /// on Add-On Instructions.
    /// </summary>
    public bool HasDependencies => HasOption("Dependencies");

    /// <summary>
    /// Returns all parsed export options.
    /// </summary>
    public IReadOnlyCollection<string> Options => _options;
}
