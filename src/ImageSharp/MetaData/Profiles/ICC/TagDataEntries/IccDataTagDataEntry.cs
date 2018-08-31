// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Text;

namespace SixLabors.ImageSharp.MetaData.Profiles.Icc
{
    /// <summary>
    /// The dataType is a simple data structure that contains
    /// either 7-bit ASCII or binary data, i.e. textType data or transparent bytes.
    /// </summary>
    internal sealed class IccDataTagDataEntry : IccTagDataEntry, IEquatable<IccDataTagDataEntry>
    {
        private static readonly Encoding AsciiEncoding = Encoding.GetEncoding("ASCII");

        /// <summary>
        /// Initializes a new instance of the <see cref="IccDataTagDataEntry"/> class.
        /// </summary>
        /// <param name="data">The raw data</param>
        public IccDataTagDataEntry(byte[] data)
            : this(data, false, IccProfileTag.Unknown)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IccDataTagDataEntry"/> class.
        /// </summary>
        /// <param name="data">The raw data</param>
        /// <param name="isAscii">True if the given data is 7bit ASCII encoded text</param>
        public IccDataTagDataEntry(byte[] data, bool isAscii)
            : this(data, isAscii, IccProfileTag.Unknown)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IccDataTagDataEntry"/> class.
        /// </summary>
        /// <param name="data">The raw data</param>
        /// <param name="isAscii">True if the given data is 7bit ASCII encoded text</param>
        /// <param name="tagSignature">Tag Signature</param>
        public IccDataTagDataEntry(byte[] data, bool isAscii, IccProfileTag tagSignature)
            : base(IccTypeSignature.Data, tagSignature)
        {
            this.Data = data ?? throw new ArgumentException(nameof(data));
            this.IsAscii = isAscii;
        }

        /// <summary>
        /// Gets the raw Data
        /// </summary>
        public byte[] Data { get; }

        /// <summary>
        /// Gets a value indicating whether the <see cref="Data"/> represents 7bit ASCII encoded text
        /// </summary>
        public bool IsAscii { get; }

        /// <summary>
        /// Gets the <see cref="Data"/> decoded as 7bit ASCII.
        /// If <see cref="IsAscii"/> is false, returns null
        /// </summary>
        public string AsciiString => this.IsAscii ? AsciiEncoding.GetString(this.Data, 0, this.Data.Length) : null;

        /// <inheritdoc/>
        public override bool Equals(IccTagDataEntry other)
        {
            return other is IccDataTagDataEntry entry && this.Equals(entry);
        }

        /// <inheritdoc/>
        public bool Equals(IccDataTagDataEntry other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return base.Equals(other) && this.Data.AsSpan().SequenceEqual(other.Data) && this.IsAscii == other.IsAscii;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is IccDataTagDataEntry other && this.Equals(other);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = base.GetHashCode();
                hashCode = (hashCode * 397) ^ (this.Data?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ this.IsAscii.GetHashCode();
                return hashCode;
            }
        }
    }
}