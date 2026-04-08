using System.Xml.Linq;
using System.Xml.XPath;
using L5xploderLib.Interfaces;
using L5xploderLib.Models;
using L5xploderLib.Services;
using L5xploderLib.Transformation;

namespace L5xploderLib;

public static class L5xExploder
{
    public static void Explode(
        Stream xmlStream,
        IEnumerable<L5xExploderConfig> configs,
        IPersistenceService persistenceService)
    {
        var xmlDoc = XDocument.Load(xmlStream);
        var filePathRegistry = new FilePathRegistry();
        var rootElement = xmlDoc.Root;
        if (rootElement == null || rootElement.Name.LocalName != Constants.RootElementName)
        {
            throw new InvalidDataException($"The XML document does not have a <{Constants.RootElementName}> root element.");
        }

        ValidateExportOptions(rootElement, persistenceService.SerializationOptions);

        new RootElementTransformer()
            .Transform(rootElement, persistenceService.SerializationOptions);

        var elementFiles = ProcessConfigs(rootElement!, string.Empty, configs, filePathRegistry, persistenceService);
        persistenceService.Save(xmlDoc, elementFiles);
    }

    /// <summary>
    /// Validates that the L5X export options are compatible with the serialization options.
    /// If the L5X was exported without the "Dependencies" option AND contains encrypted/encoded
    /// AOIs (EncodedData elements), explicit AOI dependency information is not available. In this
    /// case, the user must opt in by providing the --unsafe-skip-dependency-check flag.
    /// When there are no encoded AOIs, all dependencies can be inferred from the plain
    /// AddOnInstruction elements, so the check is not required.
    /// </summary>
    private static void ValidateExportOptions(XElement rootElement, L5xSerializationOptions serializationOptions)
    {
        var exportOptions = L5xExportOptions.FromRootElement(rootElement);

        if (exportOptions.HasDependencies || serializationOptions.UnsafeSkipDependencyCheck)
        {
            return;
        }

        // Check whether the L5X contains any EncodedData elements (encrypted/encoded AOIs)
        // under AddOnInstructionDefinitions.  If there are none, all inter-AOI dependencies
        // can be resolved from LocalTags/Parameters, so the Dependencies export option is
        // not required.
        var aoiContainer = rootElement
            .Element("Controller")?
            .Element("AddOnInstructionDefinitions");

        var hasEncodedAois = aoiContainer?
            .Elements("EncodedData")
            .Any() ?? false;

        if (hasEncodedAois)
        {
            throw new InvalidOperationException(
                "The L5X file was exported without the 'Dependencies' export option. " +
                "Without explicit dependency information, encrypted/encoded Add-On Instructions " +
                "may be imported in the wrong order, causing import errors in Logix Designer. " +
                Environment.NewLine + Environment.NewLine +
                "To resolve this, either:" + Environment.NewLine +
                "  1. Re-export the L5X from Logix Designer via the Logix Designer SDK 2.2 or newer, or" + Environment.NewLine +
                "  2. Use the --unsafe-skip-dependency-check flag to proceed without dependency " +
                "information (without dependency information, code merges may produce an L5X without proper AOI ordering).");
        }
    }

    private static IEnumerable<ElementFile> ProcessConfigs(
        XElement parentElement,
        string relativeOutputDir,
        IEnumerable<L5xExploderConfig> configs,
        FilePathRegistry filePathRegistry,
        IPersistenceService persistenceService)
    {
        var results = new List<ElementFile>();

        foreach (var config in configs)
        {
            // Avoid potentially modifying the collection while iterating (.remove) is why we're using .ToList.
            var matchingElements = parentElement.XPathSelectElements(config.XPath).ToList();

            // Run any pre-explode transform on the full collection before individual processing.
            config.PreExplodeTransform?.Invoke(matchingElements, persistenceService.SerializationOptions);

            foreach (var element in matchingElements)
            {
                results.AddRange(ProcessElement(element, relativeOutputDir, config, filePathRegistry, persistenceService));
                element.Remove();
            }
        }

        return results;
    }

    private static IEnumerable<ElementFile> ProcessElement(
        XElement element,
        string relativeOutputDir,
        L5xExploderConfig config,
        FilePathRegistry filePathRegistry,
        IPersistenceService persistenceService)
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
            var childResults = ProcessConfigs(element, elementFolder, config.ChildConfigs!, filePathRegistry, persistenceService);
            results.AddRange(childResults);
        }

        // Run any transformations
        config.Transformers?.ToList().ForEach(transformer => transformer.Transform(element, persistenceService.SerializationOptions));

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