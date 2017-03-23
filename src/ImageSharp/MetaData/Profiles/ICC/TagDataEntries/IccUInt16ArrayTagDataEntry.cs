// <copyright file="IccUInt16ArrayTagDataEntry.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System.Linq;

    /// <summary>
    /// This type represents an array of unsigned shorts.
    /// </summary>
    internal sealed class IccUInt16ArrayTagDataEntry : IccTagDataEntry
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IccUInt16ArrayTagDataEntry"/> class.
        /// </summary>
        /// <param name="data">The array data</param>
        public IccUInt16ArrayTagDataEntry(ushort[] data)
            : this(data, IccProfileTag.Unknown)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IccUInt16ArrayTagDataEntry"/> class.
        /// </summary>
        /// <param name="data">The array data</param>
        /// <param name="tagSignature">Tag Signature</param>
        public IccUInt16ArrayTagDataEntry(ushort[] data, IccProfileTag tagSignature)
            : base(IccTypeSignature.UInt16Array, tagSignature)
        {
            Guard.NotNull(data, nameof(data));
            this.Data = data;
        }

        /// <summary>
        /// Gets the array data
        /// </summary>
        public ushort[] Data { get; }

        /// <inheritdoc />
        public override bool Equals(IccTagDataEntry other)
        {
            if (base.Equals(other) && other is IccUInt16ArrayTagDataEntry entry)
            {
                return this.Data.SequenceEqual(entry.Data);
            }

            return false;
        }
    }
}
