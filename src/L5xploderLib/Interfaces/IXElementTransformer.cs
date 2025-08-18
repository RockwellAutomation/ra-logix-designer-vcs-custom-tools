using System.Xml.Linq;
using L5xploderLib;

namespace L5xploderLib.Interfaces;

public interface IXElementTransformer
{
    /// <summary>
    /// Transforms the given XElement into a (potentially) different representation.
    /// Typically used when there is redundant information in the element which can be recalculated from the remaining contents. e.g. line numbers.
    /// </summary>
    void Transform(XElement element, L5xSerializationOptions options);

    /// <summary>
    /// Inverse of the transform operation.
    /// </summary>
    void UnTransform(XElement element, L5xSerializationOptions options);
}