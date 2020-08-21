// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc
{
    /// <summary>
    /// Entry of ICC tag table
    /// </summary>
    internal readonly struct IccTagTableEntry : IEquatable<IccTagTableEntry>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IccTagTableEntry"/> struct.
        /// </summary>
        /// <param name="signature">Signature of the tag</param>
        /// <param name="offset">Offset of entry in bytes</param>
        /// <param name="dataSize">Size of entry in bytes</param>
        public IccTagTableEntry(IccProfileTag signature, uint offset, uint dataSize)
        {
            this.Signature = signature;
            this.Offset = offset;
            this.DataSize = dataSize;
        }

        /// <summary>
        /// Gets the signature of the tag.
        /// </summary>
        public IccProfileTag Signature { get; }

        /// <summary>
        /// Gets the offset of entry in bytes.
        /// </summary>
        public uint Offset { get; }

        /// <summary>
        /// Gets the size of entry in bytes.
        /// </summary>
        public uint DataSize { get; }

        /// <summary>
        /// Compares two <see cref="IccTagTableEntry"/> objects for equality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="IccTagTableEntry"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="IccTagTableEntry"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator ==(IccTagTableEntry left, IccTagTableEntry right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Compares two <see cref="IccTagTableEntry"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="IccTagTableEntry"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="IccTagTableEntry"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator !=(IccTagTableEntry left, IccTagTableEntry right)
        {
            return !left.Equals(right);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is IccTagTableEntry other && this.Equals(other);

        /// <inheritdoc/>
        public bool Equals(IccTagTableEntry other) =>
            this.Signature.Equals(other.Signature) &&
            this.Offset.Equals(other.Offset) &&
            this.DataSize.Equals(other.DataSize);

        /// <inheritdoc/>
        public override int GetHashCode() => HashCode.Combine(this.Signature, this.Offset, this.DataSize);

        /// <inheritdoc/>
        public override string ToString() => $"{this.Signature} (Offset: {this.Offset}; Size: {this.DataSize})";
    }
}
