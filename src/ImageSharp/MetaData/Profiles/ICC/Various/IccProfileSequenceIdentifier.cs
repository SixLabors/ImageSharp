// <copyright file="IccProfileSequenceIdentifier.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Linq;

    /// <summary>
    /// Description of a profile within a sequence
    /// </summary>
    internal sealed class IccProfileSequenceIdentifier : IEquatable<IccProfileSequenceIdentifier>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IccProfileSequenceIdentifier"/> class.
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
        /// Gets the ID of the profile
        /// </summary>
        public IccProfileId Id { get; }

        /// <summary>
        /// Gets the description of the profile
        /// </summary>
        public IccLocalizedString[] Description { get; }

        /// <inheritdoc />
        public bool Equals(IccProfileSequenceIdentifier other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return this.Id.Equals(other.Id) && this.Description.SequenceEqual(other.Description);
        }

        /// <inheritdoc />
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

            return obj is IccProfileSequenceIdentifier && this.Equals((IccProfileSequenceIdentifier)obj);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                return (this.Id.GetHashCode() * 397) ^ (this.Description != null ? this.Description.GetHashCode() : 0);
            }
        }
    }
}
