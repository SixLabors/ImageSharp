// <copyright file="IccColorantOrderTagDataEntry.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Linq;

    /// <summary>
    /// This tag specifies the laydown order in which colorants
    /// will be printed on an n-colorant device.
    /// </summary>
    internal sealed class IccColorantOrderTagDataEntry : IccTagDataEntry, IEquatable<IccColorantOrderTagDataEntry>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IccColorantOrderTagDataEntry"/> class.
        /// </summary>
        /// <param name="colorantNumber">Colorant order numbers</param>
        public IccColorantOrderTagDataEntry(byte[] colorantNumber)
            : this(colorantNumber, IccProfileTag.Unknown)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IccColorantOrderTagDataEntry"/> class.
        /// </summary>
        /// <param name="colorantNumber">Colorant order numbers</param>
        /// <param name="tagSignature">Tag Signature</param>
        public IccColorantOrderTagDataEntry(byte[] colorantNumber, IccProfileTag tagSignature)
            : base(IccTypeSignature.ColorantOrder, tagSignature)
        {
            Guard.NotNull(colorantNumber, nameof(colorantNumber));
            Guard.MustBeBetweenOrEqualTo(colorantNumber.Length, 1, 15, nameof(colorantNumber));

            this.ColorantNumber = colorantNumber;
        }

        /// <summary>
        /// Gets the colorant order numbers
        /// </summary>
        public byte[] ColorantNumber { get; }

        /// <inheritdoc/>
        public override bool Equals(IccTagDataEntry other)
        {
            var entry = other as IccColorantOrderTagDataEntry;
            return entry != null && this.Equals(entry);
        }

        /// <inheritdoc/>
        public bool Equals(IccColorantOrderTagDataEntry other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return base.Equals(other) && this.ColorantNumber.SequenceEqual(other.ColorantNumber);
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

            return obj is IccColorantOrderTagDataEntry && this.Equals((IccColorantOrderTagDataEntry)obj);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                return (base.GetHashCode() * 397) ^ (this.ColorantNumber != null ? this.ColorantNumber.GetHashCode() : 0);
            }
        }
    }
}