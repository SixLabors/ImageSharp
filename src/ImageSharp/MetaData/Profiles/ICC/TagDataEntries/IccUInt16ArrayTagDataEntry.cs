// <copyright file="IccUInt16ArrayTagDataEntry.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Linq;

    /// <summary>
    /// This type represents an array of unsigned shorts.
    /// </summary>
    internal sealed class IccUInt16ArrayTagDataEntry : IccTagDataEntry, IEquatable<IccUInt16ArrayTagDataEntry>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IccUInt16ArrayTagDataEntry"/> class.
        /// </summary>
        /// <param name="data">The array data</param>
        public IccUInt16ArrayTagDataEntry(ushort[] data)
            : this(data, IccProfileTag.Unknown)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IccUInt16ArrayTagDataEntry"/> class.
        /// </summary>
        /// <param name="data">The array data</param>
        /// <param name="tagSignature">Tag Signature</param>
        public IccUInt16ArrayTagDataEntry(ushort[] data, IccProfileTag tagSignature)
            : base(IccTypeSignature.UInt16Array, tagSignature)
        {
            Guard.NotNull(data, nameof(data));
            this.Data = data;
        }

        /// <summary>
        /// Gets the array data
        /// </summary>
        public ushort[] Data { get; }

        /// <inheritdoc/>
        public override bool Equals(IccTagDataEntry other)
        {
            var entry = other as IccUInt16ArrayTagDataEntry;
            return entry != null && this.Equals(entry);
        }

        /// <inheritdoc/>
        public bool Equals(IccUInt16ArrayTagDataEntry other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return base.Equals(other) && this.Data.SequenceEqual(other.Data);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            return obj is IccUInt16ArrayTagDataEntry && this.Equals((IccUInt16ArrayTagDataEntry)obj);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                return (base.GetHashCode() * 397) ^ (this.Data != null ? this.Data.GetHashCode() : 0);
            }
        }
    }
}
