// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Linq;

namespace SixLabors.ImageSharp.MetaData.Profiles.Icc
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
            Guard.NotNull(description, nameof(description));

            this.Id = id;
            this.Description = description;
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
            this.Id.Equals(other.Id) &&
            this.Description.SequenceEqual(other.Description);

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is IccProfileSequenceIdentifier other && this.Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                return (this.Id.GetHashCode() * 397) ^ (this.Description?.GetHashCode() ?? 0);
            }
        }
    }
}
