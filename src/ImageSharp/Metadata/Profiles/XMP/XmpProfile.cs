// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace SixLabors.ImageSharp.Metadata.Profiles.Xmp;

/// <summary>
/// Represents an XMP profile, providing access to the raw XML.
/// See <seealso href="https://www.adobe.com/devnet/xmp.html"/> for the full specification.
/// </summary>
public sealed class XmpProfile : IDeepCloneable<XmpProfile>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="XmpProfile"/> class.
    /// </summary>
    public XmpProfile()
        : this((byte[]?)null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="XmpProfile"/> class.
    /// </summary>
    /// <param name="data">The UTF8 encoded byte array to read the XMP profile from.</param>
    public XmpProfile(byte[]? data) => this.Data = NormalizeDataIfNeeded(data);

    /// <summary>
    /// Initializes a new instance of the <see cref="XmpProfile"/> class from an XML document.
    /// The document is serialized as UTF-8 without BOM.
    /// </summary>
    /// <param name="document">The XMP XML document.</param>
    public XmpProfile(XDocument document)
    {
        Guard.NotNull(document, nameof(document));
        this.Data = SerializeDocument(document);
    }

    /// <summary>
    /// Gets the XMP raw data byte array.
    /// </summary>
    internal byte[]? Data { get; private set; }

    /// <summary>
    /// Convert the content of this <see cref="XmpProfile"/> into an <see cref="XDocument"/>.
    /// </summary>
    /// <returns>The <see cref="XDocument"/> instance, or <see langword="null"/> if no XMP data is present.</returns>
    public XDocument? ToXDocument()
    {
        byte[]? data = this.Data;
        if (data is null || data.Length == 0)
        {
            return null;
        }

        using MemoryStream stream = new(data, writable: false);

        XmlReaderSettings settings = new()
        {
            DtdProcessing = DtdProcessing.Ignore,
            XmlResolver = null,
            CloseInput = false
        };

        using XmlReader reader = XmlReader.Create(stream, settings);
        return XDocument.Load(reader, LoadOptions.PreserveWhitespace);
    }

    /// <summary>
    /// Convert the content of this <see cref="XmpProfile"/> into a byte array.
    /// </summary>
    /// <returns>The <see cref="T:Byte[]"/></returns>
    public byte[] ToByteArray()
    {
        byte[]? data = this.Data;

        if (data is null)
        {
            return [];
        }

        byte[] result = new byte[data.Length];
        this.Data.AsSpan().CopyTo(result);
        return result;
    }

    /// <inheritdoc/>
    public XmpProfile DeepClone()
    {
        byte[]? data = this.Data;
        if (data is null)
        {
            // Preserve the semantics of an "empty" profile when cloning.
            return new XmpProfile();
        }

        byte[] clone = new byte[data.Length];
        data.AsSpan().CopyTo(clone);
        return new XmpProfile(clone);
    }

    private static byte[] SerializeDocument(XDocument document)
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

    private static byte[]? NormalizeDataIfNeeded(byte[]? data)
    {
        if (data is null || data.Length == 0)
        {
            return data;
        }

        // Allocation-free fast path for the normal case.

        // Check for UTF-8 BOM (0xEF,0xBB,0xBF)
        bool hasBom = data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF;

        // XMP metadata is commonly stored in fixed-size container blocks (e.g. TIFF tag 700).
        // Producers often pad unused space so the packet can be updated in-place without
        // rewriting the file. In practice this padding is either NUL (0x00) from the container
        // or 0x0F used by Adobe XMP writers. Both are invalid XML and must be trimmed.
        bool hasTrailingPad = data[^1] is 0 or 0x0F;

        if (!hasBom && !hasTrailingPad)
        {
            return data;
        }

        int start = hasBom ? 3 : 0;
        int end = data.Length;

        if (hasTrailingPad)
        {
            while (end > start)
            {
                byte b = data[end - 1];
                if (b is not 0 and not 0x0F)
                {
                    break;
                }

                end--;
            }
        }

        int length = end - start;
        if (length <= 0)
        {
            return null;
        }

        byte[] normalized = new byte[length];
        Buffer.BlockCopy(data, start, normalized, 0, length);
        return normalized;
    }
}
