using L5xploderLib.Enum;
using YamlDotNet.Serialization;

namespace L5xploderLib;

public sealed class L5xSerializationOptions
{
    [YamlMember(Alias = "serialization_format")]
    public L5xSerializationFormat Format { get; init; }

    [YamlMember(Alias = "xml_attribute_per_line")]
    public bool PrettyXmlAttributes { get; init; }

    [YamlMember(Alias = "omit_export_date")]
    public bool OmitExportDate { get; init; }

    public static L5xSerializationOptions DefaultOptions => new()
    {
        PrettyXmlAttributes = false,
        Format = L5xSerializationFormat.Xml,
        OmitExportDate = true,
    };

    public void Save(string filePath)
    {
        var serializer = new SerializerBuilder()
            .WithIndentedSequences()
            .Build();

        var yaml = serializer.Serialize(this);
        File.WriteAllText(filePath, yaml);
    }

    public static L5xSerializationOptions? LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        var yaml = File.ReadAllText(filePath);
        var deserializer = new DeserializerBuilder().Build();
        return deserializer.Deserialize<L5xSerializationOptions>(yaml);
    }
}