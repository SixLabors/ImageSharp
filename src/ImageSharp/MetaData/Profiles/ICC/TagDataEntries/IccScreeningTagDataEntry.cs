// <copyright file="IccScreeningTagDataEntry.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Linq;

    /// <summary>
    /// This type describes various screening parameters including
    /// screen frequency, screening angle, and spot shape.
    /// </summary>
    internal sealed class IccScreeningTagDataEntry : IccTagDataEntry, IEquatable<IccScreeningTagDataEntry>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IccScreeningTagDataEntry"/> class.
        /// </summary>
        /// <param name="flags">Screening flags</param>
        /// <param name="channels">Channel information</param>
        public IccScreeningTagDataEntry(IccScreeningFlag flags, IccScreeningChannel[] channels)
            : this(flags, channels, IccProfileTag.Unknown)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IccScreeningTagDataEntry"/> class.
        /// </summary>
        /// <param name="flags">Screening flags</param>
        /// <param name="channels">Channel information</param>
        /// <param name="tagSignature">Tag Signature</param>
        public IccScreeningTagDataEntry(IccScreeningFlag flags, IccScreeningChannel[] channels, IccProfileTag tagSignature)
            : base(IccTypeSignature.Screening, tagSignature)
        {
            Guard.NotNull(channels, nameof(channels));

            this.Flags = flags;
            this.Channels = channels;
        }

        /// <summary>
        /// Gets the screening flags
        /// </summary>
        public IccScreeningFlag Flags { get; }

        /// <summary>
        /// Gets the channel information
        /// </summary>
        public IccScreeningChannel[] Channels { get; }

        /// <inheritdoc />
        public override bool Equals(IccTagDataEntry other)
        {
            if (base.Equals(other) && other is IccScreeningTagDataEntry entry)
            {
                return this.Flags == entry.Flags
                    && this.Channels.SequenceEqual(entry.Channels);
            }

            return false;
        }

        /// <inheritdoc />
        public bool Equals(IccScreeningTagDataEntry other)
        {
            return this.Equals((IccTagDataEntry)other);
        }
    }
}
