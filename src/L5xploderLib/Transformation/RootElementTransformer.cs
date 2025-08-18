using System.Xml.Linq;
using System.Xml.XPath;
using L5xploderLib.Interfaces;

namespace L5xploderLib.Transformation;

internal sealed class RootElementTransformer : IXElementTransformer
{
    public void UnTransform(XElement element, L5xSerializationOptions options)
    {
    }


    public void Transform(XElement element, L5xSerializationOptions options)
    {
        if (options.OmitExportDate)
        {
            element.Attribute("ExportDate")?.Remove();
            element.XPathSelectElements("Controller")?.Attributes("LastModifiedDate")?.Remove();
        }
    }
}