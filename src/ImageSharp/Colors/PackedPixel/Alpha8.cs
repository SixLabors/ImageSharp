// <copyright file="Alpha8.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Numerics;

    /// <summary>
    /// Packed pixel type containing a single 8 bit normalized W values that is ranging from 0 to 1.
    /// </summary>
    public struct Alpha8 : IPackedPixel<byte>, IEquatable<Alpha8>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Alpha8"/> struct.
        /// </summary>
        /// <param name="alpha">The alpha component</param>
        public Alpha8(float alpha)
        {
            this.PackedValue = Pack(alpha);
        }

        /// <inheritdoc />
        public byte PackedValue { get; set; }

        /// <summary>
        /// Compares two <see cref="Alpha8"/> objects for equality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="Alpha8"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="Alpha8"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator ==(Alpha8 left, Alpha8 right)
        {
            return left.PackedValue == right.PackedValue;
        }

        /// <summary>
        /// Compares two <see cref="Alpha8"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="Alpha8"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="Alpha8"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator !=(Alpha8 left, Alpha8 right)
        {
            return left.PackedValue != right.PackedValue;
        }

        /// <inheritdoc />
        public void PackFromVector4(Vector4 vector)
        {
            this.PackedValue = Pack(vector.W);
        }

        /// <inheritdoc />
        public Vector4 ToVector4()
        {
            return new Vector4(0, 0, 0, this.PackedValue / 255F);
        }

        /// <inheritdoc />
        public void PackFromBytes(byte x, byte y, byte z, byte w)
        {
            this.PackedValue = w;
        }

        /// <inheritdoc />
        public void ToXyzBytes(byte[] bytes, int startIndex)
        {
            bytes[startIndex] = 0;
            bytes[startIndex + 1] = 0;
            bytes[startIndex + 2] = 0;
        }

        /// <inheritdoc />
        public void ToXyzwBytes(byte[] bytes, int startIndex)
        {
            bytes[startIndex] = 0;
            bytes[startIndex + 1] = 0;
            bytes[startIndex + 2] = 0;
            bytes[startIndex + 3] = this.PackedValue;
        }

        /// <inheritdoc />
        public void ToZyxBytes(byte[] bytes, int startIndex)
        {
            bytes[startIndex] = 0;
            bytes[startIndex + 1] = 0;
            bytes[startIndex + 2] = 0;
        }

        /// <inheritdoc />
        public void ToZyxwBytes(byte[] bytes, int startIndex)
        {
            bytes[startIndex] = 0;
            bytes[startIndex + 1] = 0;
            bytes[startIndex + 2] = 0;
            bytes[startIndex + 3] = this.PackedValue;
        }

        /// <summary>
        /// Compares an object with the packed vector.
        /// </summary>
        /// <param name="obj">The object to compare.</param>
        /// <returns>True if the object is equal to the packed vector.</returns>
        public override bool Equals(object obj)
        {
            return (obj is Alpha8) && this.Equals((Alpha8)obj);
        }

        /// <summary>
        /// Compares another Alpha8 packed vector with the packed vector.
        /// </summary>
        /// <param name="other">The Alpha8 packed vector to compare.</param>
        /// <returns>True if the packed vectors are equal.</returns>
        public bool Equals(Alpha8 other)
        {
            return this.PackedValue == other.PackedValue;
        }

        /// <summary>
        /// Gets a string representation of the packed vector.
        /// </summary>
        /// <returns>A string representation of the packed vector.</returns>
        public override string ToString()
        {
            return (this.PackedValue / 255F).ToString();
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return this.PackedValue.GetHashCode();
        }

        /// <summary>
        /// Packs a <see cref="float"/> into a byte.
        /// </summary>
        /// <param name="alpha">The float containing the value to pack.</param>
        /// <returns>The <see cref="byte"/> containing the packed values.</returns>
        private static byte Pack(float alpha)
        {
            return (byte)Math.Round(alpha.Clamp(0, 1) * 255F);
        }
    }
}