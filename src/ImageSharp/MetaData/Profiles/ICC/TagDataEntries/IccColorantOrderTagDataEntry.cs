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

        /// <inheritdoc />
        public override bool Equals(IccTagDataEntry other)
        {
            if (base.Equals(other) && other is IccColorantOrderTagDataEntry entry)
            {
                return this.ColorantNumber.SequenceEqual(entry.ColorantNumber);
            }

            return false;
        }

        /// <inheritdoc />
        public bool Equals(IccColorantOrderTagDataEntry other)
        {
            return this.Equals((IccTagDataEntry)other);
        }
    }
}
