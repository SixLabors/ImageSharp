// <copyright file="Bgr565.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Numerics;

    /// <summary>
    /// Packed pixel type containing unsigned normalized values ranging from 0 to 1. The x and z components use 5 bits, and the y component uses 6 bits.
    /// </summary>
    public struct Bgr565 : IPackedPixel<ushort>, IEquatable<Bgr565>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Bgr565"/> struct.
        /// </summary>
        /// <param name="x">The x-component</param>
        /// <param name="y">The y-component</param>
        /// <param name="z">The z-component</param>
        public Bgr565(float x, float y, float z)
        {
            this.PackedValue = Pack(x, y, z);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Bgr565"/> struct.
        /// </summary>
        /// <param name="vector">
        /// The vector containing the components for the packed value.
        /// </param>
        public Bgr565(Vector3 vector)
        {
            this.PackedValue = Pack(vector.X, vector.Y, vector.Z);
        }

        /// <inheritdoc />
        public ushort PackedValue { get; set; }

        /// <summary>
        /// Compares two <see cref="Bgr565"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="Bgr565"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="Bgr565"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator ==(Bgr565 left, Bgr565 right)
        {
            return left.PackedValue == right.PackedValue;
        }

        /// <summary>
        /// Compares two <see cref="Bgr565"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="Bgr565"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="Bgr565"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator !=(Bgr565 left, Bgr565 right)
        {
            return left.PackedValue != right.PackedValue;
        }

        /// <summary>
        /// Expands the packed representation into a <see cref="Vector3"/>.
        /// The vector components are typically expanded in least to greatest significance order.
        /// </summary>
        /// <returns>The <see cref="Vector3"/>.</returns>
        public Vector3 ToVector3()
        {
            return new Vector3(
                       ((this.PackedValue >> 11) & 0x1F) * (1F / 31F),
                       ((this.PackedValue >> 5) & 0x3F) * (1F / 63F),
                       (this.PackedValue & 0x1F) * (1F / 31F));
        }

        /// <inheritdoc />
        public void PackFromVector4(Vector4 vector)
        {
            this.PackedValue = Pack(vector.X, vector.Y, vector.Z);
        }

        /// <inheritdoc />
        public Vector4 ToVector4()
        {
            return new Vector4(this.ToVector3(), 1F);
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
            return (obj is Bgr565) && this.Equals((Bgr565)obj);
        }

        /// <inheritdoc />
        public bool Equals(Bgr565 other)
        {
            return this.PackedValue == other.PackedValue;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return this.ToVector3().ToString();
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return this.PackedValue.GetHashCode();
        }

        /// <summary>
        /// Packs the <see cref="float"/> components into a <see cref="ushort"/>.
        /// </summary>
        /// <param name="x">The x-component</param>
        /// <param name="y">The y-component</param>
        /// <param name="z">The z-component</param>
        /// <returns>The <see cref="ushort"/> containing the packed values.</returns>
        private static ushort Pack(float x, float y, float z)
        {
            return (ushort)((((int)Math.Round(x.Clamp(0, 1) * 31F) & 0x1F) << 11) |
                   (((int)Math.Round(y.Clamp(0, 1) * 63F) & 0x3F) << 5) |
                   ((int)Math.Round(z.Clamp(0, 1) * 31F) & 0x1F));
        }
    }
}