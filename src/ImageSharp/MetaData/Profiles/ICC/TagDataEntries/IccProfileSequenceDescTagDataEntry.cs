// <copyright file="IccProfileSequenceDescTagDataEntry.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Linq;

    /// <summary>
    /// This type is an array of structures, each of which contains information
    /// from the header fields and tags from the original profiles which were
    /// combined to create the final profile.
    /// </summary>
    internal sealed class IccProfileSequenceDescTagDataEntry : IccTagDataEntry, IEquatable<IccProfileSequenceDescTagDataEntry>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IccProfileSequenceDescTagDataEntry"/> class.
        /// </summary>
        /// <param name="descriptions">Profile Descriptions</param>
        public IccProfileSequenceDescTagDataEntry(IccProfileDescription[] descriptions)
            : this(descriptions, IccProfileTag.Unknown)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IccProfileSequenceDescTagDataEntry"/> class.
        /// </summary>
        /// <param name="descriptions">Profile Descriptions</param>
        /// <param name="tagSignature">Tag Signature</param>
        public IccProfileSequenceDescTagDataEntry(IccProfileDescription[] descriptions, IccProfileTag tagSignature)
            : base(IccTypeSignature.ProfileSequenceDesc, tagSignature)
        {
            Guard.NotNull(descriptions, nameof(descriptions));
            this.Descriptions = descriptions;
        }

        /// <summary>
        /// Gets the profile descriptions
        /// </summary>
        public IccProfileDescription[] Descriptions { get; }

        /// <inheritdoc />
        public override bool Equals(IccTagDataEntry other)
        {
            if (base.Equals(other) && other is IccProfileSequenceDescTagDataEntry entry)
            {
                return this.Descriptions.SequenceEqual(entry.Descriptions);
            }

            return false;
        }

        /// <inheritdoc />
        public bool Equals(IccProfileSequenceDescTagDataEntry other)
        {
            return this.Equals((IccTagDataEntry)other);
        }
    }
}
