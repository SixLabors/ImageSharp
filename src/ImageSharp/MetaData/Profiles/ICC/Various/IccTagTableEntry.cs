// <copyright file="IccTagTableEntry.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;

    /// <summary>
    /// Entry of ICC tag table
    /// </summary>
    internal struct IccTagTableEntry : IEquatable<IccTagTableEntry>
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
        /// Gets the signature of the tag
        /// </summary>
        public IccProfileTag Signature { get; }

        /// <summary>
        /// Gets the offset of entry in bytes
        /// </summary>
        public uint Offset { get; }

        /// <summary>
        /// Gets the size of entry in bytes
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
        public override bool Equals(object other)
        {
            return (other is IccTagTableEntry) && this.Equals((IccTagTableEntry)other);
        }

        /// <inheritdoc/>
        public bool Equals(IccTagTableEntry other)
        {
            return this.Signature == other.Signature
                && this.Offset == other.Offset
                && this.DataSize == other.DataSize;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = this.Signature.GetHashCode();
                hashCode = (hashCode * 397) ^ this.Offset.GetHashCode();
                hashCode = (hashCode * 397) ^ this.DataSize.GetHashCode();
                return hashCode;
            }
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"{this.Signature} (Offset: {this.Offset}; Size: {this.DataSize})";
        }
    }
}
