// <copyright file="IccDateTimeTagDataEntry.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;

    /// <summary>
    /// This type is a representation of the time and date.
    /// </summary>
    internal sealed class IccDateTimeTagDataEntry : IccTagDataEntry
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

        /// <inheritdoc />
        public override bool Equals(IccTagDataEntry other)
        {
            if (base.Equals(other) && other is IccDateTimeTagDataEntry entry)
            {
                return this.Value == entry.Value;
            }

            return false;
        }
    }
}
