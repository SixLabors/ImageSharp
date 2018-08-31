// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.MetaData.Profiles.Icc
{
    /// <summary>
    /// Entry of ICC colorant table
    /// </summary>
    internal readonly struct IccColorantTableEntry : IEquatable<IccColorantTableEntry>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IccColorantTableEntry"/> struct.
        /// </summary>
        /// <param name="name">Name of the colorant</param>
        public IccColorantTableEntry(string name)
            : this(name, 0, 0, 0)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IccColorantTableEntry"/> struct.
        /// </summary>
        /// <param name="name">Name of the colorant</param>
        /// <param name="pcs1">First PCS value</param>
        /// <param name="pcs2">Second PCS value</param>
        /// <param name="pcs3">Third PCS value</param>
        public IccColorantTableEntry(string name, ushort pcs1, ushort pcs2, ushort pcs3)
        {
            this.Name = name ?? throw new ArgumentNullException(nameof(name));
            this.Pcs1 = pcs1;
            this.Pcs2 = pcs2;
            this.Pcs3 = pcs3;
        }

        /// <summary>
        /// Gets the colorant name
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the first PCS value
        /// </summary>
        public ushort Pcs1 { get; }

        /// <summary>
        /// Gets the second PCS value
        /// </summary>
        public ushort Pcs2 { get; }

        /// <summary>
        /// Gets the third PCS value
        /// </summary>
        public ushort Pcs3 { get; }

        /// <summary>
        /// Compares two <see cref="IccColorantTableEntry"/> objects for equality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="IccColorantTableEntry"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="IccColorantTableEntry"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator ==(IccColorantTableEntry left, IccColorantTableEntry right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Compares two <see cref="IccColorantTableEntry"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="IccColorantTableEntry"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="IccColorantTableEntry"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator !=(IccColorantTableEntry left, IccColorantTableEntry right)
        {
            return !left.Equals(right);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is IccColorantTableEntry other && this.Equals(other);
        }

        /// <inheritdoc/>
        public bool Equals(IccColorantTableEntry other)
        {
            return this.Name == other.Name
                && this.Pcs1 == other.Pcs1
                && this.Pcs2 == other.Pcs2
                && this.Pcs3 == other.Pcs3;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = this.Name.GetHashCode();
                hashCode = (hashCode * 397) ^ this.Pcs1.GetHashCode();
                hashCode = (hashCode * 397) ^ this.Pcs2.GetHashCode();
                hashCode = (hashCode * 397) ^ this.Pcs3.GetHashCode();
                return hashCode;
            }
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"{this.Name}: {this.Pcs1}; {this.Pcs2}; {this.Pcs3}";
        }
    }
}
