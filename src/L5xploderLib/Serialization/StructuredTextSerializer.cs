
using System.Xml.Linq;
using L5xploderLib.Models;
using L5xploderLib.Services;

namespace L5xploderLib.Serialization;

internal sealed class StructuredTextSerializer : ICustomSerializer
{
    private string fileExt => Constants.StructuredTextFileExtension;

    public IEnumerable<XElement> Deserialize(string folderPath)
    {
        var results = new List<XElement>();

        var stFiles = Directory.GetFiles(folderPath, $"*{fileExt}");
        var routineNames = stFiles
            .Select(GetRoutineName)
            .Distinct();

        foreach (var routineName in routineNames)
        {
            var routineElement = new XElement("Routine",
                new XAttribute("Name", routineName),
                new XAttribute("Type", "ST")
                );

            foreach (var stFile in stFiles.Where(file => GetRoutineName(file) == routineName))
            {
                var content = File.ReadAllText(stFile);
                var onlineEditType = GetOnlineEditType(stFile);

                var stContentElement = new XElement("STContent");
                if (onlineEditType is not null)
                {
                    stContentElement.SetAttributeValue("OnlineEditType", onlineEditType);
                }

                // For each line in the content (regardless of Windows or Unix-style endings), create a <Line> element
                var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                foreach (var (line, lineNum) in lines.Select((line, index) => (line, index)))
                {
                    var lineElement = new XElement("Line",
                        new XAttribute("Number", lineNum),
                        new XCData(line)
                    );

                    stContentElement.Add(lineElement);
                }

                routineElement.Add(stContentElement);
            }

            results.Add(routineElement);
        }

        return results;
    }

    public IEnumerable<ElementFile> Serialize(XElement element, string elementBaseFile)
    {
        var results = new List<ElementFile>();
        var fileRegistry = new FilePathRegistry();

        var parentFolder = Path.GetDirectoryName(elementBaseFile) ?? string.Empty;

        // Find all <STContent> elements
        var stContentElements = element.Elements("STContent").ToList();

        // If there is no <STContent>, this is a no-op, just process the element as passed in.
        if (!stContentElements.Any())
        {
            return [new L5xElementFile { BaseFilePath = elementBaseFile, Element = element }];
        }

        foreach (var stContentElement in stContentElements)
        {
            // It's possible to have multiple <STContent> elements if it was edited online.
            var onlineEditType = stContentElement.Attribute("OnlineEditType")?.Value;

            // Generate the file path and name for the .st file
            var filePath = onlineEditType == null
                ? $"{elementBaseFile}"
                : $"{elementBaseFile}.{onlineEditType}";
            var fileName = Path.GetFileName(filePath);

            // Ensure the file path is unique, if not throw.  We cannot rename these to make them unique
            // because the file name is meaningful.
            if (fileRegistry.IsReserved(filePath))
            {
                throw new InvalidDataException(
                    $"The file path {filePath} is already used and cannot be used for structured text content."
                );
            }

            fileRegistry.Reserve(filePath);

            // If any line elements are missing a line number or out of order, throw an error.
            int lineNumber = 0;
            foreach (var line in stContentElement.Elements("Line"))
            {
                if (!(line.Attribute("Number")?.Value == lineNumber.ToString()))
                {
                    throw new InvalidDataException(
                        $"Line number mismatch in <STContent> element {elementBaseFile}. Expected line number {lineNumber}, but found {line.Attribute("Number")?.Value ?? "missing"}."
                    );
                }
                lineNumber++;
            }

            // Extract the content from <Line> elements now that we know they are well-ordered.
            var lines = stContentElement.Elements("Line")
                .Select(line => line.Value)
                .ToList();

            //
            // We no longer keep the XML file with a link to the stContent file, so no need to mutate the XElement.
            // Just left this for now in case we change our minds.
            //
            // // Mutate the XElement to remove the <Line> elements
            // stContentElement.Elements("Line").Remove();

            // // Set the attribute for the structured text content file
            // stContentElement.SetAttributeValue("stContentFile", Path.GetFileName(fileName));

            // Create the .st file with the content
            results.Add(
                new CustomElementFile
                {
                    BaseFilePath = filePath,
                    FileExt = fileExt,
                    Content = string.Join(Environment.NewLine, lines),
                }
            );
        }

        return results;
    }

    private static string? GetOnlineEditType(string filePath)
    {
        // Extract the online edit type from the file name if it exists
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var parts = fileName.Split('.');

        return parts != null && parts.Length > 1 ? parts[1] : null;
    }
    
    private static string GetRoutineName(string filePath)
    {
        // Extract the routine name from the file name
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var parts = fileName.Split('.');

        return parts.Length > 0 ? parts[0] : throw new InvalidDataException($"Invalid structured text file name: {filePath}");
    }
}