// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc
{
    /// <summary>
    /// The XYZType contains an array of XYZ values.
    /// </summary>
    internal sealed class IccXyzTagDataEntry : IccTagDataEntry, IEquatable<IccXyzTagDataEntry>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IccXyzTagDataEntry"/> class.
        /// </summary>
        /// <param name="data">The XYZ numbers.</param>
        public IccXyzTagDataEntry(Vector3[] data)
            : this(data, IccProfileTag.Unknown)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IccXyzTagDataEntry"/> class.
        /// </summary>
        /// <param name="data">The XYZ numbers</param>
        /// <param name="tagSignature">Tag Signature</param>
        public IccXyzTagDataEntry(Vector3[] data, IccProfileTag tagSignature)
            : base(IccTypeSignature.Xyz, tagSignature)
        {
            this.Data = data ?? throw new ArgumentNullException(nameof(data));
        }

        /// <summary>
        /// Gets the XYZ numbers.
        /// </summary>
        public Vector3[] Data { get; }

        /// <inheritdoc />
        public override bool Equals(IccTagDataEntry other)
        {
            if (base.Equals(other) && other is IccXyzTagDataEntry entry)
            {
                return this.Data.AsSpan().SequenceEqual(entry.Data);
            }

            return false;
        }

        /// <inheritdoc />
        public bool Equals(IccXyzTagDataEntry other)
        {
            return this.Equals((IccTagDataEntry)other);
        }
    }
}
