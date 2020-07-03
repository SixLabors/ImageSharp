// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc
{
    /// <summary>
    /// This type represents an array of unsigned 32bit integers.
    /// </summary>
    internal sealed class IccUInt32ArrayTagDataEntry : IccTagDataEntry, IEquatable<IccUInt32ArrayTagDataEntry>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IccUInt32ArrayTagDataEntry"/> class.
        /// </summary>
        /// <param name="data">The array data</param>
        public IccUInt32ArrayTagDataEntry(uint[] data)
            : this(data, IccProfileTag.Unknown)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IccUInt32ArrayTagDataEntry"/> class.
        /// </summary>
        /// <param name="data">The array data</param>
        /// <param name="tagSignature">Tag Signature</param>
        public IccUInt32ArrayTagDataEntry(uint[] data, IccProfileTag tagSignature)
            : base(IccTypeSignature.UInt32Array, tagSignature)
        {
            this.Data = data ?? throw new ArgumentNullException(nameof(data));
        }

        /// <summary>
        /// Gets the array data
        /// </summary>
        public uint[] Data { get; }

        /// <inheritdoc/>
        public override bool Equals(IccTagDataEntry other)
        {
            return other is IccUInt32ArrayTagDataEntry entry && this.Equals(entry);
        }

        /// <inheritdoc/>
        public bool Equals(IccUInt32ArrayTagDataEntry other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return base.Equals(other) && this.Data.AsSpan().SequenceEqual(other.Data);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is IccUInt32ArrayTagDataEntry other && this.Equals(other);
        }

        /// <inheritdoc/>
        public override int GetHashCode() => HashCode.Combine(this.Signature, this.Data);
    }
}