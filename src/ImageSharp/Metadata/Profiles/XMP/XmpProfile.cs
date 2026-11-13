// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

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
    public XmpProfile(byte[]? data) => this.Data = XmpReader.ReadPacket(data);

    /// <summary>
    /// Initializes a new instance of the <see cref="XmpProfile"/> class from an XML document.
    /// The document is serialized as UTF-8 without BOM.
    /// </summary>
    /// <param name="document">The XMP XML document.</param>
    public XmpProfile(XDocument document)
    {
        Guard.NotNull(document, nameof(document));
        this.Data = XmpWriter.WriteDocument(document);
    }

    /// <summary>
    /// Gets the XMP raw data byte array.
    /// </summary>
    internal byte[]? Data { get; private set; }

    /// <summary>
    /// Convert the content of this <see cref="XmpProfile"/> into an <see cref="XDocument"/>.
    /// </summary>
    /// <returns>The <see cref="XDocument"/> instance, or <see langword="null"/> if no XMP data is present.</returns>
    public XDocument? ToXDocument() => XmpReader.ReadDocument(this.Data);

    /// <summary>
    /// Checks that the profile contains an XMP packet.
    /// </summary>
    /// <returns>Whether the packet is nonempty.</returns>
    internal bool Validate() => this.Data?.Length > 0;

    /// <summary>
    /// Gets the packet bytes for an encoder.
    /// </summary>
    /// <returns>The existing packet, or null when it is empty.</returns>
    internal byte[]? GetDataForEncoding() => this.Validate() ? this.Data : null;

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
}
