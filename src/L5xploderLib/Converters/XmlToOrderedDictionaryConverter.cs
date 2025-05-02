using System.Collections.Specialized;
using System.Xml.Linq;

namespace L5xploderLib.Converters;

internal static class XmlToOrderedDictionaryConverter
{
    public static OrderedDictionary Convert(XDocument document)
    {
        var dict = new OrderedDictionary();

        if (document.Declaration != null)
        {
            var declaration = new OrderedDictionary
            {
                ["Version"] = document.Declaration.Version,
                ["Encoding"] = document.Declaration.Encoding,
                ["Standalone"] = document.Declaration.Standalone
            };
            dict["#declaration"] = declaration;
        }

        if (document.Root != null)
        {
            dict["#root"] = Convert(document.Root);
        }

        return dict;
    }

    public static OrderedDictionary Convert(XElement element)
    {
        // Unfortunately XML doesn't simply convert to order dictionaries and collections.
        // The format has concepts like attributes, element types, text, and CDATA, so we
        // have to create reserved keys to capture this additional structural xml information
        var dict = new OrderedDictionary();

        // Add the element name as #type
        dict["#type"] = element.Name.LocalName;

        // Add attributes as a #attributes collection
        if (element.HasAttributes)
        {
            var attributes = new OrderedDictionary();

            // Add the 'Name' attribute first if it exists
            var nameAttribute = element.Attribute("Name");
            if (nameAttribute != null)
            {
                attributes["Name"] = nameAttribute.Value;
            }

            // Add the remaining attributes, excluding 'Name'
            foreach (var attribute in element.Attributes().Where(a => a.Name.LocalName != "Name"))
            {
                attributes[attribute.Name.LocalName] = attribute.Value;
            }

            dict["#attributes"] = attributes;
        }

        // Add text or CDATA as #text or #cdata, CDATA takes precedence (CDATA is a special type of text node)
        if (!string.IsNullOrWhiteSpace(element.Value))
        {
            var cdataNode = element.Nodes().OfType<XCData>().FirstOrDefault();
            if (cdataNode != null)
            {
                dict["#cdata"] = cdataNode.Value;
            }
            else
            {
                var textNode = element.Nodes().OfType<XText>().FirstOrDefault();
                if (textNode != null)
                {
                    dict["#text"] = textNode.Value;
                }
            }
        }

        // Process child elements recursively into a #elements collection
        var childElements = element.Elements().Select(element => Convert(element)).ToList();
        if (childElements.Any())
        {
            dict["#elements"] = childElements;
        }

        return dict;
    }
}