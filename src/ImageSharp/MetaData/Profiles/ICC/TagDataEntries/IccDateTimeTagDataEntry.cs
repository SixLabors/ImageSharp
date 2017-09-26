// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.MetaData.Profiles.Icc
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
            var entry = other as IccDateTimeTagDataEntry;
            return entry != null && this.Equals(entry);
        }

        /// <inheritdoc/>
        public bool Equals(IccDateTimeTagDataEntry other)
        {
            if (ReferenceEquals(null, other))
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
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            return obj is IccDateTimeTagDataEntry && this.Equals((IccDateTimeTagDataEntry)obj);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                return (base.GetHashCode() * 397) ^ this.Value.GetHashCode();
            }
        }
    }
}