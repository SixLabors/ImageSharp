// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Text;

namespace SixLabors.ImageSharp.Metadata.Profiles.Iptc
{
    /// <summary>
    /// Represents a single value of the IPTC profile.
    /// </summary>
    public sealed class IptcValue : IDeepCloneable<IptcValue>
    {
        private byte[] data = Array.Empty<byte>();
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
            this.Strict = other.Strict;
        }

        internal IptcValue(IptcTag tag, byte[] value, bool strict)
        {
            Guard.NotNull(value, nameof(value));

            this.Strict = strict;
            this.Tag = tag;
            this.data = value;
            this.encoding = Encoding.UTF8;
        }

        internal IptcValue(IptcTag tag, Encoding encoding, string value, bool strict)
        {
            this.Strict = strict;
            this.Tag = tag;
            this.encoding = encoding;
            this.Value = value;
        }

        internal IptcValue(IptcTag tag, string value, bool strict)
        {
            this.Strict = strict;
            this.Tag = tag;
            this.encoding = Encoding.UTF8;
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
        /// Gets or sets a value indicating whether to be enforce value length restrictions according
        /// to the specification.
        /// </summary>
        public bool Strict { get; set; }

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
                    this.data = Array.Empty<byte>();
                }
                else
                {
                    int maxLength = this.Tag.MaxLength();
                    byte[] valueBytes;
                    if (this.Strict && value.Length > maxLength)
                    {
                        var cappedValue = value.Substring(0, maxLength);
                        valueBytes = this.encoding.GetBytes(cappedValue);

                        // It is still possible that the bytes of the string exceed the limit.
                        if (valueBytes.Length > maxLength)
                        {
                            throw new ArgumentException($"The iptc value exceeds the limit of {maxLength} bytes for the tag {this.Tag}");
                        }
                    }
                    else
                    {
                        valueBytes = this.encoding.GetBytes(value);
                    }

                    this.data = valueBytes;
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

            if (this.data.Length != other.data.Length)
            {
                return false;
            }

            for (int i = 0; i < this.data.Length; i++)
            {
                if (this.data[i] != other.data[i])
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
        /// <param name="encoding">The encoding to use.</param>
        /// <returns>A string that represents the current value with the specified encoding.</returns>
        public string ToString(Encoding encoding)
        {
            Guard.NotNull(encoding, nameof(encoding));

            return encoding.GetString(this.data);
        }
    }
}
