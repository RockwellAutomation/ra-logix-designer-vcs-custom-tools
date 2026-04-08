using System.Xml.Linq;
using L5xploderLib.Serialization;
using L5xploderLib.Transformation;

namespace L5xploderLib;

public static class L5xDefaultConfig
{
    public static IEnumerable<L5xExploderConfig> DefaultConfig { get; } =
    [
        new L5xExploderConfig
        {
            XPath = @"Controller/DataTypes/*",
            FolderGenerator = element => "DataTypes",
            BaseFileNameGenerator = DefaultNameGenerator,
        },
        new L5xExploderConfig
        {
            XPath = @"Controller/Modules/*",
            FolderGenerator = element => "Modules",
            BaseFileNameGenerator = DefaultNameGenerator,
            SortFunction = SortModules,
        },
        new L5xExploderConfig
        {
            XPath = @"Controller/AddOnInstructionDefinitions/*",
            FolderGenerator = element => "AddOnInstructionDefinitions",
            BaseFileNameGenerator = DefaultNameGenerator,
            SortFunction = SortAddOnInstructions,
            PreExplodeTransform = AddOriginalOrderingHints,
            ChildConfigs =
            [
                // We purposely do not break apart the Parameters and Local Tags because
                // the order they are in can impact the packing of structs. We'd rather not
                // potentially mutate the original order of these elements. This is a problem
                // because instances of this AOI may have stored values persisted elsewhere in
                // the L5x which no longer match the order of the parameters/tags in the AOI definition.
                // Not breaking them apart does not solve the problem if the AOI is manually modified,
                // but it does prevent this tool itself from breaking this parameter/tag struct.
                //
                // new L5xExploderConfig
                // {
                //     XPath = @"Parameters/*",
                //     FolderGenerator = element => "Parameters",
                //     BaseFileNameGenerator = DefaultNameGenerator,
                // },
                // new L5xExploderConfig
                // {
                //     XPath = @"LocalTags/*",
                //     FolderGenerator = element => "LocalTags",
                //     BaseFileNameGenerator = DefaultNameGenerator,
                // },
                new L5xExploderConfig
                {
                    XPath = @"Routines/*",
                    FolderGenerator = element => "Routines",
                    BaseFileNameGenerator = DefaultNameGenerator,
                    CustomSerializers = [
                        new StructuredTextSerializer(),
                    ],
                    Transformers = [
                        new LadderLogicLineNumberTransformer(),
                    ],
                },

            ],
        },
        new L5xExploderConfig
        {
            XPath = @"Controller/AlarmDefinitions/*",
            FolderGenerator = element => "AlarmDefinitions",
            BaseFileNameGenerator = DefaultNameGenerator,
        },
        new L5xExploderConfig
        {
            XPath = @"Controller/Tags/*",
            FolderGenerator = element => "Tags",
            BaseFileNameGenerator = DefaultNameGenerator,
        },
        new L5xExploderConfig
        {
            XPath = @"Controller/Programs/*",
            FolderGenerator = element => "Programs",
            BaseFileNameGenerator = DefaultNameGenerator,
            ChildConfigs = 
            [
                new L5xExploderConfig
                {
                    XPath = @"Tags/*",
                    FolderGenerator = element => "Tags",
                    BaseFileNameGenerator = DefaultNameGenerator,
                },
                new L5xExploderConfig
                {
                    XPath = @"Routines/*",
                    FolderGenerator = element => "Routines",
                    BaseFileNameGenerator = DefaultNameGenerator,
                    CustomSerializers = [
                        new StructuredTextSerializer(),
                    ],
                    Transformers = [
                        new LadderLogicLineNumberTransformer(),
                    ],
                }
            ],
        },
        new L5xExploderConfig
        {
            XPath = @"Controller/Tasks/*",
            FolderGenerator = element => "Tasks",
            BaseFileNameGenerator = DefaultNameGenerator,
        },
        new L5xExploderConfig
        {
            XPath = @"Controller/Trends/*",
            FolderGenerator = element => "Trends",
            BaseFileNameGenerator = DefaultNameGenerator,
        },
        new L5xExploderConfig
        {
            XPath = @"Controller/DataLogs/*",
            FolderGenerator = element => "DataLogs",
            BaseFileNameGenerator = DefaultNameGenerator,
        }
    ];

    private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();

    private static string DefaultNameGenerator(XElement element)
    {
        var baseName = element.Attribute("Name")?.Value;
        if (string.IsNullOrEmpty(baseName))
        {
            return "unnamed_element";
        }

        foreach (var invalidChar in InvalidFileNameChars)
        {
            baseName = baseName.Replace(invalidChar, '_');
        }

        return baseName;
    }

    /// <summary>
    /// This got rather complex.
    /// 
    /// Attempts to sort Add-On Instructions into dependency order during implode so that every AOI
    /// appears after the AOIs it depends on.  L5X files can contain 
    /// obscured, encoded content.  Additionally, there are differences between exporting an L5X file
    /// via the LD GUI, and the LD SDK.  Version 2.2.* of the LD SDK exports additional hints for
    /// dependency resolution, which we should use if present, but we have to also handle the scenario
    /// when they are not present.  The dependency resolution is imperfect/best effort for L5X files 
    /// with encoded content and no explicit Dependencies, particularly when merging, adding/removing AOIs
    /// 
    /// In short, nothing except dependency resolution with the explicit Dependencies elements
    /// produced by Logix Designer SDK 2.2* or higher can guarantee correct ordering of AOI's.
    ///
    /// Dependencies are discovered from explicit Dependencies elements when present, 
    /// or inferred from LocalTag / Parameter DataType references to other AOIs in the
    /// same collection.  When the project was originally exploded with --unsafe-skip-dependency-check,
    /// L5XGitPrevAOI ordering hints are used as a tiebreaker to preserve the
    /// original Logix Designer export order (important for encrypted AOIs whose
    /// internal dependencies are not visible, but they were exported without Dependency elements).
    ///
    /// Unnamed AOIs which cannot participate in the dependency graph are appended at the end.
    /// </summary>
    private static IList<XElement> SortAddOnInstructions(IEnumerable<XElement> addOnInstructions)
    {
        var aoiList = addOnInstructions.ToList();

        // A dictionary of AOIs by their Name attribute for quick lookup
        var aoiLookup = aoiList
            .Where(aoi => !string.IsNullOrEmpty(aoi.Attribute("Name")?.Value))
            .ToDictionary(
                aoi => aoi.Attribute("Name")!.Value,
                aoi => aoi
            );

        // Build the original ordering chain from L5XGitPrevAOI hints.
        // Maps each AOI name to the name of the AOI that should precede it.
        var prevAoiHints = new Dictionary<string, string>();
        foreach (var aoi in aoiList)
        {
            var aoiName = aoi.Attribute("Name")?.Value;
            var hintElement = aoi.Element(Constants.OriginalOrderingHintElement);
            var prevName = hintElement?.Attribute("Name")?.Value;

            if (!string.IsNullOrEmpty(aoiName) && !string.IsNullOrEmpty(prevName))
            {
                prevAoiHints[aoiName] = prevName!;
            }
        }

        // Build a dependency graph and unnamed AOI's list
        // The element which is the key depends on the elements in the value list.
        var dependencies = new Dictionary<XElement, IList<XElement>>();
        var unnamedAois = new List<XElement>();

        foreach (var aoi in aoiList)
        {
            var aoiName = aoi.Attribute("Name")?.Value;

            // If the AOI has no name, add it to the unnamed list to be appended last
            // Nothing can take a dependency on unnamed AOIs because there is no name to reference
            if (string.IsNullOrEmpty(aoiName))
            {
                unnamedAois.Add(aoi);
                continue;
            }

            // If the AOI isn't yet in the dependencies map, initialize an entry for it.
            if (!dependencies.ContainsKey(aoi))
            {
                dependencies[aoi] = new List<XElement>();
            }

            var dependenciesList = Enumerable.Empty<string?>();
            var hasExplicitDependenciesElement = aoi.Element("Dependencies") != null;

            if (hasExplicitDependenciesElement)
            {
                // If we have an explicit <Dependencies> element, use that to determine inter-aoi dependencies
                dependenciesList = aoi
                    .Element("Dependencies")?
                    .Elements("Dependency")?
                    .Where(dep => dep.Attribute("Type")?.Value == "AddOnInstructionDefinition")
                    .Select(dep => dep.Attribute("Name")?.Value)
                    .Where(name => !string.IsNullOrEmpty(name))
                    ?? Enumerable.Empty<string?>();
            }
            else
            {
                // If no explicit dependencies, find any implicit dependencies between AOIs.
                // Check both LocalTags and Parameters for DataType references to other AOIs.
                // LocalTags may not be visible for encrypted/encoded AOIs (EncodedData elements),
                // but Parameters are always visible.
                var localTagDeps = aoi
                    .Element("LocalTags")?
                    .Elements("LocalTag")
                    .Select(tag => tag.Attribute("DataType")?.Value)
                    .Where(dataType => !string.IsNullOrEmpty(dataType))
                    .Where(dataType => aoiLookup.ContainsKey(dataType!))
                    ?? Enumerable.Empty<string?>();

                var parameterDeps = aoi
                    .Element("Parameters")?
                    .Elements("Parameter")
                    .Select(param => param.Attribute("DataType")?.Value)
                    .Where(dataType => !string.IsNullOrEmpty(dataType))
                    .Where(dataType => aoiLookup.ContainsKey(dataType!))
                    ?? Enumerable.Empty<string?>();

                dependenciesList = localTagDeps.Concat(parameterDeps).Distinct();
            }

            foreach (var dependencyName in dependenciesList)
            {
                // If the required DataType is another AOI, it is a dependency
                if (!string.IsNullOrEmpty(dependencyName) && aoiLookup.TryGetValue(dependencyName, out var requiredAoi) && requiredAoi != aoi)
                {
                    // And finally, add the dependency to the map
                    dependencies[aoi].Add(requiredAoi);
                }
            }
        }

        // Reconstruct the original ordering from the L5XGitPrevAOI hint chain.
        // This gives us a baseline ordering that respects the original Logix Designer export order,
        // which accounts for dependencies that may be invisible (e.g. encrypted AOIs).
        var originalOrder = ReconstructOriginalOrder(prevAoiHints, aoiLookup);

        // Topological sort using explicit dependencies, with original ordering as tiebreaker
        var sortedAois = TopologicalSortWithPreferredOrder(dependencies, originalOrder);

        // Strip the ordering hint elements from the output — they are ephemeral
        foreach (var aoi in sortedAois)
        {
            aoi.Element(Constants.OriginalOrderingHintElement)?.Remove();
        }

        sortedAois.AddRange(unnamedAois);

        return sortedAois;
    }

    /// <summary>
    /// Adds L5XGitPrevAOI hint elements to each AOI to record the original ordering
    /// from the source L5X file. This runs during explode before elements are persisted.
    /// Hints are only added when UnsafeSkipDependencyCheck is enabled in the serialization options,
    /// which indicates the L5X was exported without the Dependencies option.
    /// </summary>
    private static void AddOriginalOrderingHints(IList<XElement> elements, L5xSerializationOptions options)
    {
        if (!options.UnsafeSkipDependencyCheck)
        {
            return;
        }

        for (int i = 1; i < elements.Count; i++)
        {
            var prevName = elements[i - 1].Attribute("Name")?.Value;
            if (!string.IsNullOrEmpty(prevName))
            {
                // Remove any existing hint (in case of re-explode)
                elements[i].Element(Constants.OriginalOrderingHintElement)?.Remove();

                // Insert the hint as the first child element so it's easy to find
                // and clearly not part of the Logix Designer schema
                elements[i].AddFirst(new XElement(Constants.OriginalOrderingHintElement, new XAttribute("Name", prevName)));
            }
        }
    }

    /// <summary>
    /// Reconstructs the original ordering of AOIs from the L5XGitPrevAOI hint chain.
    /// Returns a dictionary mapping each AOI XElement to its preferred position index.
    /// Handles broken chains gracefully (e.g. after a merge where some AOIs were removed).
    /// </summary>
    private static Dictionary<XElement, int> ReconstructOriginalOrder(
        Dictionary<string, string> prevAoiHints,
        Dictionary<string, XElement> aoiLookup)
    {
        var orderMap = new Dictionary<XElement, int>();

        if (prevAoiHints.Count == 0)
        {
            return orderMap;
        }

        // Build a forward chain: for each AOI, what AOI comes after it?
        var nextAoi = new Dictionary<string, string>();
        var hasIncomingEdge = new HashSet<string>();

        foreach (var (aoiName, prevName) in prevAoiHints)
        {
            // Only build chain links where both AOIs exist in the current set
            if (aoiLookup.ContainsKey(aoiName) && aoiLookup.ContainsKey(prevName))
            {
                // If prevName already has a "next", there's a conflict (two AOIs claim the same predecessor).
                // The second one wins (last-write-wins), which is a reasonable merge behavior.
                nextAoi[prevName] = aoiName;
                hasIncomingEdge.Add(aoiName);
            }
        }

        // Find chain heads: AOIs that are in aoiLookup, have no predecessor hint, or whose
        // predecessor doesn't exist in the current set
        var chainHeads = aoiLookup.Keys
            .Where(name => !hasIncomingEdge.Contains(name))
            .ToList();

        // Walk each chain to assign ordering indices
        int index = 0;
        foreach (var head in chainHeads)
        {
            var current = head;
            var visited = new HashSet<string>();
            while (!string.IsNullOrEmpty(current) && aoiLookup.ContainsKey(current) && visited.Add(current))
            {
                orderMap[aoiLookup[current]] = index++;
                nextAoi.TryGetValue(current, out current!);
            }
        }

        // Any AOIs not yet assigned (shouldn't happen, but defensive) get appended
        foreach (var aoi in aoiLookup.Values)
        {
            if (!orderMap.ContainsKey(aoi))
            {
                orderMap[aoi] = index++;
            }
        }

        return orderMap;
    }

    /// <summary>
    /// Performs a topological sort respecting explicit dependencies, using the preferred
    /// original ordering as a tiebreaker when the dependency graph allows flexibility.
    /// </summary>
    private static List<XElement> TopologicalSortWithPreferredOrder(
        IDictionary<XElement, IList<XElement>> dependencies,
        Dictionary<XElement, int> preferredOrder)
    {
        if (preferredOrder.Count == 0)
        {
            // No ordering hints available, fall back to standard topological sort
            return TopologicalSort(dependencies);
        }

        // Kahn's algorithm with a priority queue based on preferred order.
        // This ensures that when multiple nodes are available (no unresolved dependencies),
        // we pick the one with the lowest preferred order index.

        // Build in-degree map
        var inDegree = new Dictionary<XElement, int>();
        foreach (var node in dependencies.Keys)
        {
            if (!inDegree.ContainsKey(node)) inDegree[node] = 0;
            foreach (var dep in dependencies[node])
            {
                if (!inDegree.ContainsKey(dep)) inDegree[dep] = 0;
            }
        }
        foreach (var node in dependencies.Keys)
        {
            foreach (var dep in dependencies[node])
            {
                // node depends on dep, so node has an incoming edge from dep.
                // But in our graph, dependencies[node] lists what node depends ON.
                // For Kahn's, we need: for each dep, node is a dependent of dep.
                // We need to count how many things each node depends on (in-degree in reverse).
            }
        }

        // Rebuild as adjacency list: dep -> list of nodes that depend on dep
        var dependents = new Dictionary<XElement, List<XElement>>();
        foreach (var node in dependencies.Keys)
        {
            if (!dependents.ContainsKey(node)) dependents[node] = new List<XElement>();
        }
        foreach (var node in dependencies.Keys)
        {
            inDegree[node] = dependencies[node].Count;
            foreach (var dep in dependencies[node])
            {
                if (!dependents.ContainsKey(dep)) dependents[dep] = new List<XElement>();
                dependents[dep].Add(node);
            }
        }

        // Initialize with nodes that have no dependencies (in-degree 0)
        var available = new List<XElement>();

        foreach (var node in inDegree.Keys)
        {
            if (inDegree[node] == 0)
            {
                available.Add(node);
            }
        }

        int GetPreferredOrder(XElement e) => preferredOrder.TryGetValue(e, out var o) ? o : int.MaxValue;

        var sorted = new List<XElement>();
        while (available.Count > 0)
        {
            // Pick the available node with the lowest preferred order index
            var node = available.OrderBy(GetPreferredOrder).First();
            available.Remove(node);
            sorted.Add(node);

            if (dependents.TryGetValue(node, out var deps))
            {
                foreach (var dependent in deps)
                {
                    inDegree[dependent]--;
                    if (inDegree[dependent] == 0)
                    {
                        available.Add(dependent);
                    }
                }
            }
        }

        if (sorted.Count != inDegree.Count)
        {
            throw new InvalidOperationException("Cyclic dependency detected among add-on instructions.");
        }

        return sorted;
    }

    private static IList<XElement> SortModules(IEnumerable<XElement> modules)
    {
        // Build a dependency graph and unnamed modules list
        // The element which is the key depends on the elements in the value list.
        var dependencies = new Dictionary<XElement, IList<XElement>>();
        var unnamedModules = new List<XElement>();

        foreach (var module in modules)
        {
            var moduleName = module.Attribute("Name")?.Value;
            var parentModuleName = module.Attribute("ParentModule")?.Value;

            // If the module has no name, add it to the unnamed list to be appended last
            // Nothing can take a dependency on unnamed modules because there is no name to reference
            if (string.IsNullOrEmpty(moduleName))
            {
                unnamedModules.Add(module);
                continue;
            }

            // If the module isn't yet in the dependencies map, initialize an entry for it.
            if (!dependencies.ContainsKey(module))
            {
                dependencies[module] = new List<XElement>();
            }

            // Find the parent module in the list
            var parentModule = modules.FirstOrDefault(m =>
                m.Attribute("Name")?.Value == parentModuleName);

            // If the parentModule differs from the current module, it is a dependency
            if (parentModule != null && parentModule != module)
            {
                // And finally, add the dependency to the map
                dependencies[module].Add(parentModule);
            }
        }

        var sortedModules = TopologicalSort(dependencies);
        sortedModules.AddRange(unnamedModules);

        return sortedModules;
    }

    private static List<XElement> TopologicalSort(IDictionary<XElement, IList<XElement>> dependencies)
    {
        var sorted = new List<XElement>();
        var visited = new HashSet<XElement>();
        var visiting = new HashSet<XElement>();

        void Visit(XElement node)
        {
            if (visited.Contains(node)) return;
            if (visiting.Contains(node))
            {
                throw new InvalidOperationException("Cyclic dependency detected.");
            }

            visiting.Add(node);

            if (dependencies.ContainsKey(node))
            {
                foreach (var child in dependencies[node])
                {
                    Visit(child);
                }
            }

            visiting.Remove(node);
            visited.Add(node);

            sorted.Add(node);
        }

        foreach (var node in dependencies.Keys)
        {
            Visit(node);
        }

        return sorted;
    }
}
