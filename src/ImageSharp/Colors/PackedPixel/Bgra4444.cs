// <copyright file="Bgra4444.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Numerics;

    /// <summary>
    /// Packed pixel type containing unsigned normalized values, ranging from 0 to 1, using 4 bits each for x, y, z, and w.
    /// </summary>
    public struct Bgra4444 : IPackedPixel<ushort>, IEquatable<Bgra4444>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Bgra4444"/> struct.
        /// </summary>
        /// <param name="x">The x-component</param>
        /// <param name="y">The y-component</param>
        /// <param name="z">The z-component</param>
        /// <param name="w">The w-component</param>
        public Bgra4444(float x, float y, float z, float w)
        {
            this.PackedValue = Pack(x, y, z, w);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Bgra4444"/> struct.
        /// </summary>
        /// <param name="vector">The vector containing the components for the packed vector.</param>
        public Bgra4444(Vector4 vector)
        {
            this.PackedValue = Pack(vector.X, vector.Y, vector.Z, vector.W);
        }

        /// <inheritdoc />
        public ushort PackedValue { get; set; }

        /// <summary>
        /// Compares two <see cref="Bgra4444"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="Bgra4444"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="Bgra4444"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator ==(Bgra4444 left, Bgra4444 right)
        {
            return left.PackedValue == right.PackedValue;
        }

        /// <summary>
        /// Compares two <see cref="Bgra4444"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="Bgra4444"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="Bgra4444"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator !=(Bgra4444 left, Bgra4444 right)
        {
            return left.PackedValue != right.PackedValue;
        }

        /// <inheritdoc />
        public Vector4 ToVector4()
        {
            const float Max = 1 / 15F;

            return new Vector4(
                       ((this.PackedValue >> 8) & 0x0F) * Max,
                       ((this.PackedValue >> 4) & 0x0F) * Max,
                       (this.PackedValue & 0x0F) * Max,
                       ((this.PackedValue >> 12) & 0x0F) * Max);
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
            bytes[startIndex] = (byte)vector.X;
            bytes[startIndex + 1] = (byte)vector.Y;
            bytes[startIndex + 2] = (byte)vector.Z;
        }

        /// <inheritdoc />
        public void ToXyzwBytes(byte[] bytes, int startIndex)
        {
            Vector4 vector = this.ToVector4() * 255F;
            bytes[startIndex] = (byte)vector.X;
            bytes[startIndex + 1] = (byte)vector.Y;
            bytes[startIndex + 2] = (byte)vector.Z;
            bytes[startIndex + 3] = (byte)vector.W;
        }

        /// <inheritdoc />
        public void ToZyxBytes(byte[] bytes, int startIndex)
        {
            Vector4 vector = this.ToVector4() * 255F;
            bytes[startIndex] = (byte)vector.Z;
            bytes[startIndex + 1] = (byte)vector.Y;
            bytes[startIndex + 2] = (byte)vector.X;
        }

        /// <inheritdoc />
        public void ToZyxwBytes(byte[] bytes, int startIndex)
        {
            Vector4 vector = this.ToVector4() * 255F;
            bytes[startIndex] = (byte)vector.Z;
            bytes[startIndex + 1] = (byte)vector.Y;
            bytes[startIndex + 2] = (byte)vector.X;
            bytes[startIndex + 3] = (byte)vector.W;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return (obj is Bgra4444) && this.Equals((Bgra4444)obj);
        }

        /// <inheritdoc />
        public bool Equals(Bgra4444 other)
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
        /// Packs the <see cref="float"/> components into a <see cref="ushort"/>.
        /// </summary>
        /// <param name="x">The x-component</param>
        /// <param name="y">The y-component</param>
        /// <param name="z">The z-component</param>
        /// <param name="w">The w-component</param>
        /// <returns>The <see cref="ushort"/> containing the packed values.</returns>
        private static ushort Pack(float x, float y, float z, float w)
        {
            return (ushort)((((int)Math.Round(w.Clamp(0, 1) * 15F) & 0x0F) << 12) |
                (((int)Math.Round(x.Clamp(0, 1) * 15F) & 0x0F) << 8) |
                (((int)Math.Round(y.Clamp(0, 1) * 15F) & 0x0F) << 4) |
                ((int)Math.Round(z.Clamp(0, 1) * 15F) & 0x0F));
        }
    }
}
