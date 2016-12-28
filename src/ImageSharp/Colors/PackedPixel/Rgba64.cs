// <copyright file="Rgba64.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Numerics;

    /// <summary>
    /// Packed pixel type containing four 16-bit unsigned normalized values ranging from 0 to 1.
    /// </summary>
    public struct Rgba64 : IPackedPixel<ulong>, IEquatable<Rgba64>, IPackedVector
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Rgba64"/> struct.
        /// </summary>
        /// <param name="x">The x-component</param>
        /// <param name="y">The y-component</param>
        /// <param name="z">The z-component</param>
        /// <param name="w">The w-component</param>
        public Rgba64(float x, float y, float z, float w)
        {
            this.PackedValue = Pack(x, y, z, w);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Rgba64"/> struct.
        /// </summary>
        /// <param name="vector">The vector containing the components values.</param>
        public Rgba64(Vector4 vector)
        {
            this.PackedValue = Pack(vector.X, vector.Y, vector.Z, vector.W);
        }

        /// <inheritdoc />
        public ulong PackedValue { get; set; }

        /// <summary>
        /// Compares two <see cref="Rgba64"/> objects for equality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="Rgba64"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="Rgba64"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator ==(Rgba64 left, Rgba64 right)
        {
            return left.PackedValue == right.PackedValue;
        }

        /// <summary>
        /// Compares two <see cref="Rgba64"/> objects for equality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="Rgba64"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="Rgba64"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator !=(Rgba64 left, Rgba64 right)
        {
            return left.PackedValue != right.PackedValue;
        }

        /// <inheritdoc />
        public Vector4 ToVector4()
        {
            return new Vector4(
                (this.PackedValue & 0xFFFF) / 65535F,
                ((this.PackedValue >> 16) & 0xFFFF) / 65535F,
                ((this.PackedValue >> 32) & 0xFFFF) / 65535F,
                ((this.PackedValue >> 48) & 0xFFFF) / 65535F);
        }

        /// <inheritdoc />
        public void PackFromVector4(Vector4 vector)
        {
            this.PackedValue = Pack(vector.X, vector.Y, vector.Z, vector.W);
        }

        /// <inheritdoc />
        public void PackFromBytes(byte x, byte y, byte z, byte w)
        {
            this.PackFromVector4(new Vector4(x, y, z, w) / 255F);
        }

        /// <inheritdoc />
        public void ToXyzBytes(byte[] bytes, int startIndex)
        {
            Vector4 vector = this.ToVector4() * 255F;

            bytes[startIndex] = (byte)(float)Math.Round(vector.X);
            bytes[startIndex + 1] = (byte)(float)Math.Round(vector.Y);
            bytes[startIndex + 2] = (byte)(float)Math.Round(vector.Z);
        }

        /// <inheritdoc />
        public void ToXyzwBytes(byte[] bytes, int startIndex)
        {
            Vector4 vector = this.ToVector4() * 255F;

            bytes[startIndex] = (byte)(float)Math.Round(vector.X);
            bytes[startIndex + 1] = (byte)(float)Math.Round(vector.Y);
            bytes[startIndex + 2] = (byte)(float)Math.Round(vector.Z);
            bytes[startIndex + 3] = (byte)(float)Math.Round(vector.W);
        }

        /// <inheritdoc />
        public void ToZyxBytes(byte[] bytes, int startIndex)
        {
            Vector4 vector = this.ToVector4() * 255F;

            bytes[startIndex] = (byte)(float)Math.Round(vector.Z);
            bytes[startIndex + 1] = (byte)(float)Math.Round(vector.Y);
            bytes[startIndex + 2] = (byte)(float)Math.Round(vector.X);
        }

        /// <inheritdoc />
        public void ToZyxwBytes(byte[] bytes, int startIndex)
        {
            Vector4 vector = this.ToVector4() * 255F;

            bytes[startIndex] = (byte)(float)Math.Round(vector.Z);
            bytes[startIndex + 1] = (byte)(float)Math.Round(vector.Y);
            bytes[startIndex + 2] = (byte)(float)Math.Round(vector.X);
            bytes[startIndex + 3] = (byte)(float)Math.Round(vector.W);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return (obj is Rgba64) && this.Equals((Rgba64)obj);
        }

        /// <inheritdoc />
        public bool Equals(Rgba64 other)
        {
            return this.PackedValue == other.PackedValue;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return this.ToVector4().ToString();
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return this.PackedValue.GetHashCode();
        }

        /// <summary>
        /// Packs the <see cref="float"/> components into a <see cref="ulong"/>.
        /// </summary>
        /// <param name="x">The x-component</param>
        /// <param name="y">The y-component</param>
        /// <param name="z">The z-component</param>
        /// <param name="w">The w-component</param>
        /// <returns>The <see cref="ulong"/> containing the packed values.</returns>
        private static ulong Pack(float x, float y, float z, float w)
        {
            return (ulong)Math.Round(x.Clamp(0, 1) * 65535F) |
                   ((ulong)Math.Round(y.Clamp(0, 1) * 65535F) << 16) |
                   ((ulong)Math.Round(z.Clamp(0, 1) * 65535F) << 32) |
                   ((ulong)Math.Round(w.Clamp(0, 1) * 65535F) << 48);
        }
    }
}
