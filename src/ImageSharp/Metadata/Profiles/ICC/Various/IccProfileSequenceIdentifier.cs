// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc
{
    /// <summary>
    /// Description of a profile within a sequence.
    /// </summary>
    internal readonly struct IccProfileSequenceIdentifier : IEquatable<IccProfileSequenceIdentifier>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IccProfileSequenceIdentifier"/> struct.
        /// </summary>
        /// <param name="id">ID of the profile</param>
        /// <param name="description">Description of the profile</param>
        public IccProfileSequenceIdentifier(IccProfileId id, IccLocalizedString[] description)
        {
            this.Id = id;
            this.Description = description ?? throw new ArgumentNullException(nameof(description));
        }

        /// <summary>
        /// Gets the ID of the profile.
        /// </summary>
        public IccProfileId Id { get; }

        /// <summary>
        /// Gets the description of the profile.
        /// </summary>
        public IccLocalizedString[] Description { get; }

        /// <inheritdoc />
        public bool Equals(IccProfileSequenceIdentifier other) =>
            this.Id.Equals(other.Id)
            && this.Description.AsSpan().SequenceEqual(other.Description);

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is IccProfileSequenceIdentifier other && this.Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => HashCode.Combine(this.Id, this.Description);
    }
}
