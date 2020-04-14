// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Text;

namespace SixLabors.ImageSharp.Metadata.Profiles.Iptc
{
    /// <summary>
    /// A value of the iptc profile.
    /// </summary>
    public sealed class IptcValue : IDeepCloneable<IptcValue>
    {
        private byte[] data;
        private Encoding encoding;

        internal IptcValue(IptcValue other)
        {
            if (other.data != null)
            {
                this.data = new byte[other.data.Length];
                other.data.AsSpan().CopyTo(this.data);
            }

            if (other.Encoding != null)
            {
                this.Encoding = (Encoding)other.Encoding.Clone();
            }

            this.Tag = other.Tag;
        }

        internal IptcValue(IptcTag tag, byte[] value)
        {
            Guard.NotNull(value, nameof(value));

            this.Tag = tag;
            this.data = value;
            this.encoding = Encoding.UTF8;
        }

        internal IptcValue(IptcTag tag, Encoding encoding, string value)
        {
            this.Tag = tag;
            this.encoding = encoding;
            this.Value = value;
        }

        /// <summary>
        /// Gets or sets the encoding to use for the Value.
        /// </summary>
        public Encoding Encoding
        {
            get => this.encoding;
            set
            {
                if (value != null)
                {
                    this.encoding = value;
                }
            }
        }

        /// <summary>
        /// Gets the tag of the iptc value.
        /// </summary>
        public IptcTag Tag { get; }

        /// <summary>
        /// Gets or sets the value.
        /// </summary>
        public string Value
        {
            get => this.encoding.GetString(this.data);
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    this.data = new byte[0];
                }
                else
                {
                    this.data = this.encoding.GetBytes(value);
                }
            }
        }

        /// <summary>
        /// Gets the length of the value.
        /// </summary>
        public int Length => this.data.Length;

        /// <inheritdoc/>
        public IptcValue DeepClone() => new IptcValue(this);

        /// <summary>
        /// Determines whether the specified object is equal to the current <see cref="IptcValue"/>.
        /// </summary>
        /// <param name="obj">The object to compare this <see cref="IptcValue"/> with.</param>
        /// <returns>True when the specified object is equal to the current <see cref="IptcValue"/>.</returns>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            return this.Equals(obj as IptcValue);
        }

        /// <summary>
        /// Determines whether the specified iptc value is equal to the current <see cref="IptcValue"/>.
        /// </summary>
        /// <param name="other">The iptc value to compare this <see cref="IptcValue"/> with.</param>
        /// <returns>True when the specified iptc value is equal to the current <see cref="IptcValue"/>.</returns>
        public bool Equals(IptcValue other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (this.Tag != other.Tag)
            {
                return false;
            }

            byte[] data = other.ToByteArray();

            if (this.data.Length != data.Length)
            {
                return false;
            }

            for (int i = 0; i < this.data.Length; i++)
            {
                if (this.data[i] != data[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Serves as a hash of this type.
        /// </summary>
        /// <returns>A hash code for the current instance.</returns>
        public override int GetHashCode() => HashCode.Combine(this.data, this.Tag);

        /// <summary>
        /// Converts this instance to a byte array.
        /// </summary>
        /// <returns>A <see cref="byte"/> array.</returns>
        public byte[] ToByteArray()
        {
            var result = new byte[this.data.Length];
            this.data.CopyTo(result, 0);
            return result;
        }

        /// <summary>
        /// Returns a string that represents the current value.
        /// </summary>
        /// <returns>A string that represents the current value.</returns>
        public override string ToString() => this.Value;

        /// <summary>
        /// Returns a string that represents the current value with the specified encoding.
        /// </summary>
        /// <param name="enc">The encoding to use.</param>
        /// <returns>A string that represents the current value with the specified encoding.</returns>
        public string ToString(Encoding enc)
        {
            Guard.NotNull(enc, nameof(enc));

            return enc.GetString(this.data);
        }
    }
}
