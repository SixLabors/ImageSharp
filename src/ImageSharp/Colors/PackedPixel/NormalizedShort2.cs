// <copyright file="NormalizedShort2.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Numerics;

    /// <summary>
    /// Packed pixel type containing two 16-bit signed normalized values, ranging from −1 to 1.
    /// </summary>
    public struct NormalizedShort2 : IPackedPixel<uint>, IEquatable<NormalizedShort2>
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
        /// Initializes a new instance of the <see cref="NormalizedShort2"/> struct.
        /// </summary>
        /// <param name="vector">The vector containing the component values.</param>
        public NormalizedShort2(Vector2 vector)
        {
            this.PackedValue = Pack(vector.X, vector.Y);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NormalizedShort2"/> struct.
        /// </summary>
        /// <param name="x">The x-component.</param>
        /// <param name="y">The y-component.</param>
        public NormalizedShort2(float x, float y)
        {
            this.PackedValue = Pack(x, y);
        }

        /// <inheritdoc />
        public uint PackedValue { get; set; }

        /// <summary>
        /// Compares two <see cref="NormalizedShort2"/> objects for equality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="NormalizedShort2"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="NormalizedShort2"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator ==(NormalizedShort2 left, NormalizedShort2 right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Compares two <see cref="NormalizedShort2"/> objects for equality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="NormalizedShort2"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="NormalizedShort2"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator !=(NormalizedShort2 left, NormalizedShort2 right)
        {
            return !left.Equals(right);
        }

        /// <inheritdoc />
        public void PackFromVector4(Vector4 vector)
        {
            this.PackedValue = Pack(vector.X, vector.Y);
        }

        /// <inheritdoc />
        public Vector4 ToVector4()
        {
            return new Vector4(this.ToVector2(), 0, 1);
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
            bytes[startIndex + 2] = 0;
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
            bytes[startIndex + 2] = 0;
            bytes[startIndex + 3] = 255;
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

            bytes[startIndex] = 0;
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

            bytes[startIndex] = 0;
            bytes[startIndex + 1] = (byte)(float)Math.Round(vector.Y);
            bytes[startIndex + 2] = (byte)(float)Math.Round(vector.X);
            bytes[startIndex + 3] = 255;
        }

        /// <summary>
        /// Expands the packed representation into a <see cref="Vector2"/>.
        /// The vector components are typically expanded in least to greatest significance order.
        /// </summary>
        /// <returns>The <see cref="Vector2"/>.</returns>
        public Vector2 ToVector2()
        {
            const float MaxVal = 0x7FFF;

            return new Vector2(
                (short)(this.PackedValue & 0xFFFF) / MaxVal,
                (short)(this.PackedValue >> 0x10) / MaxVal);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return (obj is NormalizedShort2) && this.Equals((NormalizedShort2)obj);
        }

        /// <inheritdoc />
        public bool Equals(NormalizedShort2 other)
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
        /// Packs the <see cref="float"/> components into a <see cref="uint"/>.
        /// </summary>
        /// <param name="x">The x-component</param>
        /// <param name="y">The y-component</param>
        /// <returns>The <see cref="uint"/> containing the packed values.</returns>
        private static uint Pack(float x, float y)
        {
            const float MaxPos = 0x7FFF;
            const float MinNeg = -MaxPos;

            // Clamp the value between min and max values
            // Round rather than truncate.
            uint word2 = (uint)((int)(float)Math.Round(x * MaxPos).Clamp(MinNeg, MaxPos) & 0xFFFF);
            uint word1 = (uint)(((int)(float)Math.Round(y * MaxPos).Clamp(MinNeg, MaxPos) & 0xFFFF) << 0x10);

            return word2 | word1;
        }
    }
}