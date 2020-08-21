// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc
{
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
            => this.Descriptions = descriptions ?? throw new ArgumentNullException(nameof(descriptions));

        /// <summary>
        /// Gets the profile descriptions
        /// </summary>
        public IccProfileDescription[] Descriptions { get; }

        /// <inheritdoc/>
        public override bool Equals(IccTagDataEntry other)
            => other is IccProfileSequenceDescTagDataEntry entry && this.Equals(entry);

        /// <inheritdoc />
        public bool Equals(IccProfileSequenceDescTagDataEntry other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return base.Equals(other) && this.Descriptions.AsSpan().SequenceEqual(other.Descriptions);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
            => obj is IccProfileSequenceDescTagDataEntry other && this.Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => HashCode.Combine(this.Signature, this.Descriptions);
    }
}
