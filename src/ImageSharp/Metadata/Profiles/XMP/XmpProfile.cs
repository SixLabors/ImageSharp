// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using System.IO;
using System.Text;
using System.Xml.Linq;

namespace SixLabors.ImageSharp.Metadata.Profiles.Xmp
{
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
            : this((byte[])null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="XmpProfile"/> class.
        /// </summary>
        /// <param name="data">The UTF8 encoded byte array to read the XMP profile from.</param>
        public XmpProfile(byte[] data) => this.Data = data;

        /// <summary>
        /// Initializes a new instance of the <see cref="XmpProfile"/> class
        /// by making a copy from another XMP profile.
        /// </summary>
        /// <param name="other">The other XMP profile, from which the clone should be made from.</param>
        private XmpProfile(XmpProfile other)
        {
            Guard.NotNull(other, nameof(other));

            this.Data = other.Data;
        }

        /// <summary>
        /// Gets the XMP raw data byte array.
        /// </summary>
        internal byte[] Data { get; private set; }

        /// <summary>
        /// Gets the raw XML document containing the XMP profile.
        /// </summary>
        /// <returns>The <see cref="XDocument"/></returns>
        public XDocument GetDocument()
        {
            byte[] byteArray = this.Data;
            if (byteArray is null)
            {
                return null;
            }

            // Strip leading whitespace, as the XmlReader doesn't like them.
            int count = byteArray.Length;
            for (int i = count - 1; i > 0; i--)
            {
                if (byteArray[i] is 0 or 0x0f)
                {
                    count--;
                }
            }

            using var stream = new MemoryStream(byteArray, 0, count);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return XDocument.Load(reader);
        }

        /// <summary>
        /// Convert the content of this <see cref="XmpProfile"/> into a byte array.
        /// </summary>
        /// <returns>The <see cref="T:byte[]"/></returns>
        public byte[] ToByteArray()
        {
            byte[] result = new byte[this.Data.Length];
            this.Data.AsSpan().CopyTo(result);
            return result;
        }

        /// <inheritdoc/>
        public XmpProfile DeepClone() => new(this);
    }
}
