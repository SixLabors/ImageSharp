// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc
{
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
            return other is IccColorantOrderTagDataEntry entry && this.Equals(entry);
        }

        /// <inheritdoc/>
        public bool Equals(IccColorantOrderTagDataEntry other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return base.Equals(other) && this.ColorantNumber.AsSpan().SequenceEqual(other.ColorantNumber);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is IccColorantOrderTagDataEntry other && this.Equals(other);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return HashCode.Combine(this.Signature, this.ColorantNumber);
        }
    }
}