using System.Xml.Linq;
using System.Xml.XPath;
using L5xploderLib.Interfaces;

namespace L5xploderLib;

public static class L5xImploder
{
    public static void Implode(
        string outputFilePath,
        IEnumerable<L5xExploderConfig> configs,
        IPersistenceService persistenceService)
    {
        var xmlDoc = persistenceService.LoadRoot();
        var rootElement = xmlDoc.Root;
        ValidateRootElement(rootElement);

        ProcessConfigs(string.Empty, rootElement!, configs, persistenceService);

        xmlDoc.Save(outputFilePath);
    }

    private static void ValidateRootElement(XElement? rootElement)
    {
        if (rootElement == null || rootElement.Name.LocalName != Constants.RootElementName)
        {
            throw new InvalidDataException($"The document does not have a <{Constants.RootElementName}> root element.");
        }
    }

    private static void ProcessConfigs(string relativeDir, XElement targetElement, IEnumerable<L5xExploderConfig> configs, IPersistenceService persistenceService)
    {
        foreach (var config in configs)
        {
            ProcessConfig(relativeDir, targetElement, config, persistenceService);
        }
    }

    private static void ProcessConfig(string relativeDir, XElement targetElement, L5xExploderConfig config, IPersistenceService persistenceService)
    {
        // Get the parent node using the XPath from the config
        string parentXPath = GetParentXPath(config);

        var parentNode = targetElement.XPathSelectElement(parentXPath);
        if (parentNode == null)
        {
            // If the parent not is not found, there cannot be any children to process
            // so we can skip this config.
            return;
        }

        // Read all of the elements from the directory
        var elements = GetElementsFromDirectory(relativeDir, parentNode, config, persistenceService);

        // Apply any necessary sorting function.  The L5X format expects dependencies to be
        // defined before things which depend on them in the resultant XML file.
        if (config.SortFunction != null)
        {
            elements = config.SortFunction(elements);
        }


        // Append the sorted elements to the appropriate parent node in the document we are reconstituting.
        parentNode.Add(elements);
    }

    private static string GetParentXPath(L5xExploderConfig config)
    {
        var xPathParts = config.XPath.Split('/');
        var parentXPath = string.Join("/", xPathParts.Take(xPathParts.Length - 1));
        return parentXPath;
    }

    private static IEnumerable<XElement> GetElementsFromDirectory(string relativeDir, XElement parentNode, L5xExploderConfig config, IPersistenceService persistenceService)
    {
        var hasChildConfig = config.ChildConfigs != null && config.ChildConfigs.Any();

        // Get the folder containing the split files
        var folderPath = Path.Combine(relativeDir, config.FolderGenerator.Invoke(parentNode));
        if (!persistenceService.DirectoryExists(folderPath))
        {
            return Enumerable.Empty<XElement>();
        }

        var elements = new List<XElement>();

        elements.AddRange(persistenceService.LoadCustomSerializedElements(folderPath, config.CustomSerializers));

        if (hasChildConfig)
        {
            // If a child configuration exists files are one level deeper in thier identically named subdirectories
            var subdirectories = persistenceService.GetDirectories(folderPath);
            foreach (var subdirectory in subdirectories)
            {
                var folderName = Path.GetFileName(subdirectory); // Get the base folder name
                var elementFiles = persistenceService.GetBaseFiles(subdirectory);
                foreach (var file in elementFiles)
                {
                    // Process the file only if its basename matches the folder name (case-insensitive)
                    if (string.Equals(file, folderName, StringComparison.OrdinalIgnoreCase))
                    {
                        var relativeFilePath = Path.Combine(subdirectory, file);
                        var element = persistenceService.LoadElement(relativeFilePath);
                        ProcessConfigs(subdirectory, element, config.ChildConfigs!, persistenceService);
                        elements.Add(element);
                    }
                }
            }
        }
        else
        {
            // If no child configuration exists the element files are in the current folder
            var elementFiles = persistenceService.GetBaseFiles(folderPath);
            foreach (var file in elementFiles)
            {
                var relativeFilePath = Path.Combine(folderPath, file);
                var element = persistenceService.LoadElement(relativeFilePath);
                elements.Add(element);
            }
        }

        // Undo any transformations
        config.Transformers?.ToList().ForEach(transformer =>
        {
            foreach (var element in elements)
            {
                transformer.UnTransform(element, persistenceService.SerializationOptions);
            }
        });

        return elements;
    }
}