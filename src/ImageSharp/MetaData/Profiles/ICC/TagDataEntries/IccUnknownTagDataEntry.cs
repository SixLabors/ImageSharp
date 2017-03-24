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

        /// <inheritdoc />
        public override bool Equals(IccTagDataEntry other)
        {
            if (base.Equals(other) && other is IccUnknownTagDataEntry entry)
            {
                return this.Data.SequenceEqual(entry.Data);
            }

            return false;
        }

        /// <inheritdoc />
        public bool Equals(IccUnknownTagDataEntry other)
        {
            return this.Equals((IccTagDataEntry)other);
        }
    }
}
