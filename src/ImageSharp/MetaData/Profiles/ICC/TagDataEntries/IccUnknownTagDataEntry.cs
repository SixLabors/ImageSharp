// <copyright file="IccUnknownTagDataEntry.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Linq;

    /// <summary>
    /// This tag stores data of an unknown tag data entry
    /// </summary>
    internal sealed class IccUnknownTagDataEntry : IccTagDataEntry, IEquatable<IccUnknownTagDataEntry>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IccUnknownTagDataEntry"/> class.
        /// </summary>
        /// <param name="data">The raw data of the entry</param>
        public IccUnknownTagDataEntry(byte[] data)
            : this(data, IccProfileTag.Unknown)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IccUnknownTagDataEntry"/> class.
        /// </summary>
        /// <param name="data">The raw data of the entry</param>
        /// <param name="tagSignature">Tag Signature</param>
        public IccUnknownTagDataEntry(byte[] data, IccProfileTag tagSignature)
            : base(IccTypeSignature.Unknown, tagSignature)
        {
            Guard.NotNull(data, nameof(data));
            this.Data = data;
        }

        /// <summary>
        /// Gets the raw data of the entry
        /// </summary>
        public byte[] Data { get; }

        /// <inheritdoc/>
        public override bool Equals(IccTagDataEntry other)
        {
            var entry = other as IccUnknownTagDataEntry;
            return entry != null && this.Equals(entry);
        }

        /// <inheritdoc/>
        public bool Equals(IccUnknownTagDataEntry other)
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

            return obj is IccUnknownTagDataEntry && this.Equals((IccUnknownTagDataEntry)obj);
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