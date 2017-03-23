// <copyright file="IccFix16ArrayTagDataEntry.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System.Linq;

    /// <summary>
    /// This type represents an array of doubles (from 32bit fixed point values).
    /// </summary>
    internal sealed class IccFix16ArrayTagDataEntry : IccTagDataEntry
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IccFix16ArrayTagDataEntry"/> class.
        /// </summary>
        /// <param name="data">The array data</param>
        public IccFix16ArrayTagDataEntry(float[] data)
            : this(data, IccProfileTag.Unknown)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IccFix16ArrayTagDataEntry"/> class.
        /// </summary>
        /// <param name="data">The array data</param>
        /// <param name="tagSignature">Tag Signature</param>
        public IccFix16ArrayTagDataEntry(float[] data, IccProfileTag tagSignature)
            : base(IccTypeSignature.S15Fixed16Array, tagSignature)
        {
            Guard.NotNull(data, nameof(data));
            this.Data = data;
        }

        /// <summary>
        /// Gets the array data
        /// </summary>
        public float[] Data { get; }

        /// <inheritdoc />
        public override bool Equals(IccTagDataEntry other)
        {
            if (base.Equals(other) && other is IccFix16ArrayTagDataEntry entry)
            {
                return this.Data.SequenceEqual(entry.Data);
            }

            return false;
        }
    }
}
