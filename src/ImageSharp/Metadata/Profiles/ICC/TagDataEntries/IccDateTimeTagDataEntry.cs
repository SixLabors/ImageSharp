// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc
{
    /// <summary>
    /// This type is a representation of the time and date.
    /// </summary>
    internal sealed class IccDateTimeTagDataEntry : IccTagDataEntry, IEquatable<IccDateTimeTagDataEntry>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IccDateTimeTagDataEntry"/> class.
        /// </summary>
        /// <param name="value">The DateTime value</param>
        public IccDateTimeTagDataEntry(DateTime value)
            : this(value, IccProfileTag.Unknown)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IccDateTimeTagDataEntry"/> class.
        /// </summary>
        /// <param name="value">The DateTime value</param>
        /// <param name="tagSignature">Tag Signature</param>
        public IccDateTimeTagDataEntry(DateTime value, IccProfileTag tagSignature)
            : base(IccTypeSignature.DateTime, tagSignature)
        {
            this.Value = value;
        }

        /// <summary>
        /// Gets the date and time value
        /// </summary>
        public DateTime Value { get; }

        /// <inheritdoc/>
        public override bool Equals(IccTagDataEntry other)
        {
            return other is IccDateTimeTagDataEntry entry && this.Equals(entry);
        }

        /// <inheritdoc/>
        public bool Equals(IccDateTimeTagDataEntry other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return base.Equals(other) && this.Value.Equals(other.Value);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is IccDateTimeTagDataEntry other && this.Equals(other);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return HashCode.Combine(this.Signature, this.Value);
        }
    }
}