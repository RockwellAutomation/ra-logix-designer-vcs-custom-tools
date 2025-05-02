using System.IO;
using System.Xml;
using System.Xml.Linq;
using L5xploderLib;
using L5xploderLib.Services;

internal sealed class XmlPersistenceService : PersistenceService
{
    protected override string FileExtension => Constants.XmlFileExtension;

    private XmlWriterSettings xmlWriterSettings => new()
    {
        Indent = true,
        OmitXmlDeclaration = false,
        NewLineOnAttributes = SerializationOptions.PrettyXmlAttributes,
        NewLineHandling = NewLineHandling.None,
    };

    protected override XElement LoadElementImpl(string absoluteFilePath)
    {
        using var fileStream = new FileStream(absoluteFilePath, FileMode.Open, FileAccess.Read);
        return XElement.Load(fileStream);
    }

    protected override XDocument LoadRootImpl(string absoluteFilePath)
    {
        using var fileStream = new FileStream(absoluteFilePath, FileMode.Open, FileAccess.Read);
        return XDocument.Load(fileStream);
    }

    protected override void SaveElementImpl(XElement element, string absoluteFilePath)
    {
        using var fileStream = new FileStream(absoluteFilePath, FileMode.Create, FileAccess.Write);
        using var writer = XmlWriter.Create(fileStream, xmlWriterSettings);
        element.Save(writer);
    }

    protected override void SaveRootImpl(XDocument xmlDoc, string absoluteFilePath)
    {
        using var fileStream = new FileStream(absoluteFilePath, FileMode.Create, FileAccess.Write);
        using var writer = XmlWriter.Create(fileStream, xmlWriterSettings);
        xmlDoc.Save(writer);
    }
}