using L5xploderLib.Enum;
using L5xploderLib.Interfaces;

namespace L5xploderLib.Services;

public static class PersistenceServiceFactory
{
    public static IPersistenceService Create(L5xSerializationOptions options, string explodedDir)
    {
        switch (options.Format)
        {
            case L5xSerializationFormat.Xml:
                return new XmlPersistenceService
                {
                    SerializationOptions = options,
                    ExplodedDir = explodedDir
                };
            default:
                throw new NotSupportedException($"Serialization type {options.Format} is not supported.");
        }
    }
}