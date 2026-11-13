// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace SixLabors.ImageSharp.Metadata.Profiles.Xmp;

/// <summary>
/// Serializes XMP documents.
/// </summary>
internal static class XmpWriter
{
    /// <summary>
    /// Serializes an XMP document as UTF-8 without a byte order mark.
    /// </summary>
    /// <param name="document">The document to write.</param>
    /// <returns>The encoded packet.</returns>
    public static byte[] WriteDocument(XDocument document)
    {
        using MemoryStream ms = new();

        XmlWriterSettings writerSettings = new()
        {
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), // no BOM
            OmitXmlDeclaration = true, // generally safer for XMP consumers
            Indent = false,
            NewLineHandling = NewLineHandling.None
        };

        using (XmlWriter xw = XmlWriter.Create(ms, writerSettings))
        {
            document.Save(xw);
        }

        return ms.ToArray();
    }
}
