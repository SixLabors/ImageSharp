// <copyright file="IccProfileSequenceIdentifierTagDataEntry.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System.Linq;

    /// <summary>
    /// This type is an array of structures, each of which contains information
    /// for identification of a profile used in a sequence.
    /// </summary>
    internal sealed class IccProfileSequenceIdentifierTagDataEntry : IccTagDataEntry
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IccProfileSequenceIdentifierTagDataEntry"/> class.
        /// </summary>
        /// <param name="data">Profile Identifiers</param>
        public IccProfileSequenceIdentifierTagDataEntry(IccProfileSequenceIdentifier[] data)
            : this(data, IccProfileTag.Unknown)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IccProfileSequenceIdentifierTagDataEntry"/> class.
        /// </summary>
        /// <param name="data">Profile Identifiers</param>
        /// <param name="tagSignature">Tag Signature</param>
        public IccProfileSequenceIdentifierTagDataEntry(IccProfileSequenceIdentifier[] data, IccProfileTag tagSignature)
            : base(IccTypeSignature.ProfileSequenceIdentifier, tagSignature)
        {
            Guard.NotNull(data, nameof(data));
            this.Data = data;
        }

        /// <summary>
        /// Gets the profile identifiers
        /// </summary>
        public IccProfileSequenceIdentifier[] Data { get; }

        /// <inheritdoc />
        public override bool Equals(IccTagDataEntry other)
        {
            if (base.Equals(other) && other is IccProfileSequenceIdentifierTagDataEntry entry)
            {
                return this.Data.SequenceEqual(entry.Data);
            }

            return false;
        }
    }
}
