// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using System.Text;
using System.Xml.Linq;

namespace SixLabors.ImageSharp.Metadata.Profiles.Xmp
{
    /// <summary>
    /// Represents an XMP profile, providing access to the raw XML.
    /// </summary>
    public sealed class XmpProfile : IDeepCloneable<XmpProfile>
    {
        private XDocument document;

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

            if (other.Data != null)
            {
                this.Data = new byte[other.Data.Length];
                other.Data.AsSpan().CopyTo(this.Data);
            }
        }

        /// <summary>
        /// Gets the rax XML document containing the XMP profile.
        /// </summary>
        public XDocument Document
        {
            get
            {
                this.InitializeDocument();
                return this.document;
            }
        }

        /// <summary>
        /// Gets the byte data of the XMP profile.
        /// </summary>
        public byte[] Data { get; private set; }

        /// <summary>
        /// Checks whether two <see cref="XmpProfile"/> structures are equal.
        /// </summary>
        /// <param name="left">The left hand <see cref="XmpProfile"/> operand.</param>
        /// <param name="right">The right hand <see cref="XmpProfile"/> operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter;
        /// otherwise, false.
        /// </returns>
        public static bool operator ==(XmpProfile left, XmpProfile right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (ReferenceEquals(left, null))
            {
                return false;
            }

            return left.Equals(right);
        }

        /// <summary>
        /// Checks whether two <see cref="XmpProfile"/> structures are not equal.
        /// </summary>
        /// <param name="left">The left hand <see cref="XmpProfile"/> operand.</param>
        /// <param name="right">The right hand <see cref="XmpProfile"/> operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter;
        /// otherwise, false.
        /// </returns>
        public static bool operator !=(XmpProfile left, XmpProfile right)
        {
            return !(left == right);
        }

        /// <inheritdoc/>
        public XmpProfile DeepClone() => new(this);

        /// <summary>
        /// Updates the data of the profile.
        /// </summary>
        public void UpdateData()
        {
            if (this.document == null)
            {
                return;
            }

            using var stream = new MemoryStream(this.Data.Length);
            using var writer = new StreamWriter(stream, Encoding.UTF8);
            this.document.Save(writer);
            this.Data = stream.ToArray();
        }

        /// <inheritdoc />
        public override int GetHashCode() => base.GetHashCode();

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            XmpProfile other = obj as XmpProfile;
            if (ReferenceEquals(other, null))
            {
                return false;
            }

            if (ReferenceEquals(this.Data, null))
            {
                return false;
            }

            return this.Data.Equals(other.Data);
        }

        private void InitializeDocument()
        {
            if (this.document != null)
            {
                return;
            }

            if (this.Data == null)
            {
                return;
            }

            // Strip leading whitespace, as the XmlReader doesn't like them.
            int count = this.Data.Length;
            for (int i = count - 1; i > 0; i--)
            {
                if (this.Data[i] is 0 or 0x0f)
                {
                    count--;
                }
            }

            using var stream = new MemoryStream(this.Data, 0, count);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            this.document = XDocument.Load(reader);
        }
    }
}
