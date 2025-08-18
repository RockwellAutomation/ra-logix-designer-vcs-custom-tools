using System.Xml.Linq;
using L5xploderLib.Interfaces;

namespace L5xploderLib.Transformation;

internal sealed class LadderLogicLineNumberTransformer : IXElementTransformer
{
    public void UnTransform(XElement element, L5xSerializationOptions options)
    {
        var rllContentElements = element.Elements("RLLContent").ToList();

        foreach (var rllContentElement in rllContentElements)
        {
            var rungs = rllContentElement.Elements("Rung").ToList();

            for (int i = 0; i < rungs.Count; i++)
            {
                // Save the existing attributes, inserting "Number" at the front
                // This is how Logix Designer outputs the l5x, but the order of the attributes
                // really shouldn't matter, so I'm not sure if this is worth any performance hit.
                // If we determine it doesn't matter, the entire content of the enclosing for loop can just be:
                //
                //  rungs[i].SetAttributeValue("Number", i.ToString());
                //
                // The only reason it would matter is user preference.  The L5x importer does not care about
                // attribute order.

                var rung = rungs[i];
                var existingAttrs = rung.Attributes().ToList();
                rung.RemoveAttributes();
                rung.SetAttributeValue("Number", i.ToString());

                foreach (var attr in existingAttrs.Where(a => a.Name != "Number"))
                {
                    rung.SetAttributeValue(attr.Name, attr.Value);
                }
            }
        }
    }


    public void Transform(XElement element, L5xSerializationOptions options)
    {
        var rllContentElements = element.Elements("RLLContent").ToList();

        foreach (var rllContentElement in rllContentElements)
        {
            var rungs = rllContentElement.Elements("Rung").ToList();

            for (int i = 0; i < rungs.Count; i++)
            {
                var numberAttr = rungs[i].Attribute("Number");

                if (numberAttr == null)
                {
                    throw new InvalidOperationException($"Rung at index {i} is missing the Number attribute");
                }

                if (numberAttr.Value != i.ToString())
                {
                    throw new InvalidOperationException($"Rung at index {i} has Number='{numberAttr.Value}', expected '{i}'");
                }

                numberAttr.Remove();
            }
        }
    }
}