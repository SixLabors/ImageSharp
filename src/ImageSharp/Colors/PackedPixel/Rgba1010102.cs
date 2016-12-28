// <copyright file="Rgba1010102.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Numerics;

    /// <summary>
    /// Packed vector type containing unsigned normalized values ranging from 0 to 1.
    /// The x, y and z components use 10 bits, and the w component uses 2 bits.
    /// </summary>
    public struct Rgba1010102 : IPackedPixel<uint>, IEquatable<Rgba1010102>, IPackedVector
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Rgba1010102"/> struct.
        /// </summary>
        /// <param name="x">The x-component</param>
        /// <param name="y">The y-component</param>
        /// <param name="z">The z-component</param>
        /// <param name="w">The w-component</param>
        public Rgba1010102(float x, float y, float z, float w)
        {
            this.PackedValue = Pack(x, y, z, w);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Rgba1010102"/> struct.
        /// </summary>
        /// <param name="vector">The vector containing the component values.</param>
        public Rgba1010102(Vector4 vector)
        {
            this.PackedValue = Pack(vector.X, vector.Y, vector.Z, vector.W);
        }

        /// <inheritdoc />
        public uint PackedValue { get; set; }

        /// <summary>
        /// Compares two <see cref="Rgba1010102"/> objects for equality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="Rgba1010102"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="Rgba1010102"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator ==(Rgba1010102 left, Rgba1010102 right)
        {
            return left.PackedValue == right.PackedValue;
        }

        /// <summary>
        /// Compares two <see cref="Rgba1010102"/> objects for equality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="Rgba1010102"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="Rgba1010102"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator !=(Rgba1010102 left, Rgba1010102 right)
        {
            return left.PackedValue != right.PackedValue;
        }

        /// <inheritdoc />
        public Vector4 ToVector4()
        {
            return new Vector4(
                ((this.PackedValue >> 0) & 0x03FF) / 1023F,
                ((this.PackedValue >> 10) & 0x03FF) / 1023F,
                ((this.PackedValue >> 20) & 0x03FF) / 1023F,
                ((this.PackedValue >> 30) & 0x03) / 3F);
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
            return (obj is Rgba1010102) && this.Equals((Rgba1010102)obj);
        }

        /// <inheritdoc />
        public bool Equals(Rgba1010102 other)
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
        /// Packs the <see cref="float"/> components into a <see cref="uint"/>.
        /// </summary>
        /// <param name="x">The x-component</param>
        /// <param name="y">The y-component</param>
        /// <param name="z">The z-component</param>
        /// <param name="w">The w-component</param>
        /// <returns>The <see cref="uint"/> containing the packed values.</returns>
        private static uint Pack(float x, float y, float z, float w)
        {
            return (uint)(
                (((int)Math.Round(x.Clamp(0, 1) * 1023F) & 0x03FF) << 0) |
                (((int)Math.Round(y.Clamp(0, 1) * 1023F) & 0x03FF) << 10) |
                (((int)Math.Round(z.Clamp(0, 1) * 1023F) & 0x03FF) << 20) |
                (((int)Math.Round(w.Clamp(0, 1) * 3F) & 0x03) << 30));
        }
    }
}