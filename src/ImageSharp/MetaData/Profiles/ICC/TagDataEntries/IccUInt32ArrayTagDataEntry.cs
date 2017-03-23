// <copyright file="IccUInt32ArrayTagDataEntry.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System.Linq;

    /// <summary>
    /// This type represents an array of unsigned 32bit integers.
    /// </summary>
    internal sealed class IccUInt32ArrayTagDataEntry : IccTagDataEntry
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IccUInt32ArrayTagDataEntry"/> class.
        /// </summary>
        /// <param name="data">The array data</param>
        public IccUInt32ArrayTagDataEntry(uint[] data)
            : this(data, IccProfileTag.Unknown)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IccUInt32ArrayTagDataEntry"/> class.
        /// </summary>
        /// <param name="data">The array data</param>
        /// <param name="tagSignature">Tag Signature</param>
        public IccUInt32ArrayTagDataEntry(uint[] data, IccProfileTag tagSignature)
            : base(IccTypeSignature.UInt32Array, tagSignature)
        {
            Guard.NotNull(data, nameof(data));
            this.Data = data;
        }

        /// <summary>
        /// Gets the array data
        /// </summary>
        public uint[] Data { get; }

        /// <inheritdoc />
        public override bool Equals(IccTagDataEntry other)
        {
            if (base.Equals(other) && other is IccUInt32ArrayTagDataEntry entry)
            {
                return this.Data.SequenceEqual(entry.Data);
            }

            return false;
        }
    }
}
