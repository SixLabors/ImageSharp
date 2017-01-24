// <copyright file="NormalizedShort4.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Numerics;

    /// <summary>
    /// Packed pixel type containing four 16-bit signed normalized values, ranging from −1 to 1.
    /// </summary>
    public struct NormalizedShort4 : IPackedPixel<ulong>, IEquatable<NormalizedShort4>
    {
        /// <summary>
        /// The maximum byte value.
        /// </summary>
        private static readonly Vector4 MaxBytes = new Vector4(255);

        /// <summary>
        /// The half the maximum byte value.
        /// </summary>
        private static readonly Vector4 Half = new Vector4(127);

        /// <summary>
        /// The vector value used for rounding.
        /// </summary>
        private static readonly Vector4 Round = new Vector4(.5F);

        /// <summary>
        /// Initializes a new instance of the <see cref="NormalizedShort4"/> struct.
        /// </summary>
        /// <param name="vector">The vector containing the component values.</param>
        public NormalizedShort4(Vector4 vector)
        {
            this.PackedValue = Pack(vector.X, vector.Y, vector.Z, vector.W);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NormalizedShort4"/> struct.
        /// </summary>
        /// <param name="x">The x-component.</param>
        /// <param name="y">The y-component.</param>
        /// <param name="z">The z-component.</param>
        /// <param name="w">The w-component.</param>
        public NormalizedShort4(float x, float y, float z, float w)
        {
            this.PackedValue = Pack(x, y, z, w);
        }

        /// <inheritdoc />
        public ulong PackedValue { get; set; }

        /// <summary>
        /// Compares two <see cref="NormalizedShort4"/> objects for equality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="NormalizedShort4"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="NormalizedShort4"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator ==(NormalizedShort4 left, NormalizedShort4 right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Compares two <see cref="NormalizedShort4"/> objects for equality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="NormalizedShort4"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="NormalizedShort4"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator !=(NormalizedShort4 left, NormalizedShort4 right)
        {
            return !left.Equals(right);
        }

        /// <inheritdoc />
        public void PackFromVector4(Vector4 vector)
        {
            this.PackedValue = Pack(vector.X, vector.Y, vector.Z, vector.W);
        }

        /// <inheritdoc />
        public Vector4 ToVector4()
        {
            const float MaxVal = 0x7FFF;

            return new Vector4(
                         (short)((this.PackedValue >> 0x00) & 0xFFFF) / MaxVal,
                         (short)((this.PackedValue >> 0x10) & 0xFFFF) / MaxVal,
                         (short)((this.PackedValue >> 0x20) & 0xFFFF) / MaxVal,
                         (short)((this.PackedValue >> 0x30) & 0xFFFF) / MaxVal);
        }

        /// <inheritdoc />
        public void PackFromBytes(byte x, byte y, byte z, byte w)
        {
            Vector4 vector = new Vector4(x, y, z, w);
            vector -= Round;
            vector -= Half;
            vector -= Round;
            vector /= Half;
            this.PackFromVector4(vector);
        }

        /// <inheritdoc />
        public void ToXyzBytes(byte[] bytes, int startIndex)
        {
            Vector4 vector = this.ToVector4();
            vector *= Half;
            vector += Round;
            vector += Half;
            vector += Round;
            vector = Vector4.Clamp(vector, Vector4.Zero, MaxBytes);

            bytes[startIndex] = (byte)(float)Math.Round(vector.X);
            bytes[startIndex + 1] = (byte)(float)Math.Round(vector.Y);
            bytes[startIndex + 2] = (byte)(float)Math.Round(vector.Z);
        }

        /// <inheritdoc />
        public void ToXyzwBytes(byte[] bytes, int startIndex)
        {
            Vector4 vector = this.ToVector4();
            vector *= Half;
            vector += Round;
            vector += Half;
            vector += Round;
            vector = Vector4.Clamp(vector, Vector4.Zero, MaxBytes);

            bytes[startIndex] = (byte)(float)Math.Round(vector.X);
            bytes[startIndex + 1] = (byte)(float)Math.Round(vector.Y);
            bytes[startIndex + 2] = (byte)(float)Math.Round(vector.Z);
            bytes[startIndex + 3] = (byte)(float)Math.Round(vector.W);
        }

        /// <inheritdoc />
        public void ToZyxBytes(byte[] bytes, int startIndex)
        {
            Vector4 vector = this.ToVector4();
            vector *= Half;
            vector += Round;
            vector += Half;
            vector += Round;
            vector = Vector4.Clamp(vector, Vector4.Zero, MaxBytes);

            bytes[startIndex] = (byte)(float)Math.Round(vector.Z);
            bytes[startIndex + 1] = (byte)(float)Math.Round(vector.Y);
            bytes[startIndex + 2] = (byte)(float)Math.Round(vector.X);
        }

        /// <inheritdoc />
        public void ToZyxwBytes(byte[] bytes, int startIndex)
        {
            Vector4 vector = this.ToVector4();
            vector *= Half;
            vector += Round;
            vector += Half;
            vector += Round;
            vector = Vector4.Clamp(vector, Vector4.Zero, MaxBytes);

            bytes[startIndex] = (byte)(float)Math.Round(vector.Z);
            bytes[startIndex + 1] = (byte)(float)Math.Round(vector.Y);
            bytes[startIndex + 2] = (byte)(float)Math.Round(vector.X);
            bytes[startIndex + 3] = (byte)(float)Math.Round(vector.W);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return (obj is NormalizedShort4) && this.Equals((NormalizedShort4)obj);
        }

        /// <inheritdoc />
        public bool Equals(NormalizedShort4 other)
        {
            return this.PackedValue.Equals(other.PackedValue);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return this.PackedValue.GetHashCode();
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return this.PackedValue.ToString("X");
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
            const float MaxPos = 0x7FFF;
            const float MinNeg = -MaxPos;

            // Clamp the value between min and max values
            ulong word4 = ((ulong)(float)Math.Round(x * MaxPos).Clamp(MinNeg, MaxPos) & 0xFFFF) << 0x00;
            ulong word3 = ((ulong)(float)Math.Round(y * MaxPos).Clamp(MinNeg, MaxPos) & 0xFFFF) << 0x10;
            ulong word2 = ((ulong)(float)Math.Round(z * MaxPos).Clamp(MinNeg, MaxPos) & 0xFFFF) << 0x20;
            ulong word1 = ((ulong)(float)Math.Round(w * MaxPos).Clamp(MinNeg, MaxPos) & 0xFFFF) << 0x30;

            return word4 | word3 | word2 | word1;
        }
    }
}
