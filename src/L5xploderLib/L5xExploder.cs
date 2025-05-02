using System.Xml.Linq;
using System.Xml.XPath;
using L5xploderLib.Interfaces;
using L5xploderLib.Models;
using L5xploderLib.Services;

namespace L5xploderLib;

public static class L5xExploder
{
    public static void Explode(
        Stream xmlStream,
        IEnumerable<L5xExploderConfig> configs,
        IPersistenceService persistenceHandler)
    {
        var xmlDoc = XDocument.Load(xmlStream);
        var filePathRegistry = new FilePathRegistry();
        var rootElement = xmlDoc.Root;
        if (rootElement == null || rootElement.Name.LocalName != Constants.RootElementName)
        {
            throw new InvalidDataException($"The XML document does not have a <{Constants.RootElementName}> root element.");
        }

        if (persistenceHandler.SerializationOptions.OmitExportDate)
        {
            rootElement.Attribute("ExportDate")?.Remove();
            rootElement.XPathSelectElements("Controller")?.Attributes("LastModifiedDate")?.Remove();
        }

        var elementFiles = ProcessConfigs(rootElement!, string.Empty, configs, filePathRegistry);
        persistenceHandler.Save(xmlDoc, elementFiles);
    }

    private static IEnumerable<ElementFile> ProcessConfigs(XElement parentElement, string relativeOutputDir, IEnumerable<L5xExploderConfig> configs, FilePathRegistry filePathRegistry)
    {
        var results = new List<ElementFile>();

        foreach (var config in configs)
        {
            // Avoid potentially modifying the collection while iterating (.remove) is why we're using .ToList.
            var matchingElements = parentElement.XPathSelectElements(config.XPath).ToList();

            foreach (var element in matchingElements)
            {
                results.AddRange(ProcessElement(element, relativeOutputDir, config, filePathRegistry));
                element.Remove();
            }
        }

        return results;
    }

    private static IEnumerable<ElementFile> ProcessElement(XElement element, string relativeOutputDir, L5xExploderConfig config, FilePathRegistry filePathRegistry)
    {
        var results = new List<ElementFile>();
        var hasChildConfig = config.ChildConfigs != null && config.ChildConfigs.Any();

        string fileName = config.BaseFileNameGenerator.Invoke(element);
        string elementFolder = GetElementFolder(element, relativeOutputDir, config);
        string elementFilePath = Path.Combine(elementFolder, fileName);

        ValidateChildConfig(config, elementFilePath, filePathRegistry);

        // Process child configurations if they exist
        if (hasChildConfig)
        {
            var childResults = ProcessConfigs(element, elementFolder, config.ChildConfigs!, filePathRegistry);
            results.AddRange(childResults);
        }

        // Run any custom serializer(s)
        if (config.CustomSerializers != null)
        {
            foreach (var serializer in config.CustomSerializers)
            {
                var customFiles = serializer.Serialize(element, elementFilePath);
                results.AddRange(customFiles);
            }
        }
        else
        {
            elementFilePath = filePathRegistry.FindUnreservedFilePath(elementFolder, fileName);
            filePathRegistry.Reserve(elementFilePath);
            results.Add(new L5xElementFile
            {
                BaseFilePath = elementFilePath,
                Element = element
            });
        }

        return results;
    }

    private static void ValidateChildConfig(L5xExploderConfig parentConfig, string elementFile, FilePathRegistry filePathRegistry)
    {
        // We when have a child config, the presumption is this parent element is within a folder with the same basename as the file
        // therefore we cannot just rename the file to avoid collision, we must throw.
        if (parentConfig.ChildConfigs != null)
        {
            if (filePathRegistry.IsReserved(elementFile))
            {
                throw new InvalidOperationException($"The file {elementFile} already exists and this element type has a child configuration.");
            }
        }
    }

    private static string GetElementFolder(XElement element, string relativeOutputDir, L5xExploderConfig config)
    {
        string elementFolder = Path.Combine(relativeOutputDir, config.FolderGenerator.Invoke(element));

        var hasChildConfig = config.ChildConfigs != null && config.ChildConfigs.Any();            
        if (hasChildConfig)
        {
            string baseFileName = config.BaseFileNameGenerator.Invoke(element);

            // If the configuration has child configurations it gets a folder
            // to contain itself and those children, not just an xml file
            elementFolder = Path.Combine(elementFolder, baseFileName);
        }

        return elementFolder;
    }
}