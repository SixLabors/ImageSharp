// <copyright file="Short2.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Numerics;

    /// <summary>
    /// Packed pixel type containing two 16-bit signed integer values.
    /// </summary>
    public struct Short2 : IPackedPixel<uint>, IEquatable<Short2>
    {
        /// <summary>
        /// The maximum byte value.
        /// </summary>
        private static readonly Vector2 MaxBytes = new Vector2(255);

        /// <summary>
        /// The half the maximum byte value.
        /// </summary>
        private static readonly Vector2 Half = new Vector2(127);

        /// <summary>
        /// The vector value used for rounding.
        /// </summary>
        private static readonly Vector2 Round = new Vector2(.5F);

        /// <summary>
        /// Initializes a new instance of the <see cref="Short2"/> struct.
        /// </summary>
        /// <param name="vector">The vector containing the component values.</param>
        public Short2(Vector2 vector)
        {
            this.PackedValue = Pack(vector.X, vector.Y);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Short2"/> struct.
        /// </summary>
        /// <param name="x">The x-component.</param>
        /// <param name="y">The y-component.</param>
        public Short2(float x, float y)
        {
            this.PackedValue = Pack(x, y);
        }

        /// <inheritdoc />
        public uint PackedValue { get; set; }

        /// <summary>
        /// Compares two <see cref="Short2"/> objects for equality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="Short2"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="Short2"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator ==(Short2 left, Short2 right)
        {
            return left.PackedValue == right.PackedValue;
        }

        /// <summary>
        /// Compares two <see cref="Short2"/> objects for equality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="Short2"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="Short2"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator !=(Short2 left, Short2 right)
        {
            return left.PackedValue != right.PackedValue;
        }

        /// <inheritdoc />
        public void PackFromVector4(Vector4 vector)
        {
            this.PackedValue = Pack(vector.X, vector.Y);
        }

        /// <inheritdoc />
        public Vector4 ToVector4()
        {
            return new Vector4((short)(this.PackedValue & 0xFFFF), (short)(this.PackedValue >> 0x10), 0, 1);
        }

        /// <inheritdoc />
        public void PackFromBytes(byte x, byte y, byte z, byte w)
        {
            Vector2 vector = new Vector2(x, y) / 255;
            vector *= 65534;
            vector -= new Vector2(32767);
            this.PackedValue = Pack(vector.X, vector.Y);
        }

        /// <inheritdoc />
        public void ToXyzBytes(byte[] bytes, int startIndex)
        {
            Vector2 vector = this.ToVector2();
            vector /= 65534;
            vector *= 255;
            vector += Half;
            vector += Round;
            vector = Vector2.Clamp(vector, Vector2.Zero, MaxBytes);

            bytes[startIndex] = (byte)(float)Math.Round(vector.X);
            bytes[startIndex + 1] = (byte)(float)Math.Round(vector.Y);
            bytes[startIndex + 2] = 0;
        }

        /// <inheritdoc />
        public void ToXyzwBytes(byte[] bytes, int startIndex)
        {
            Vector2 vector = this.ToVector2();
            vector /= 65534;
            vector *= 255;
            vector += Half;
            vector += Round;
            vector = Vector2.Clamp(vector, Vector2.Zero, MaxBytes);

            bytes[startIndex] = (byte)(float)Math.Round(vector.X);
            bytes[startIndex + 1] = (byte)(float)Math.Round(vector.Y);
            bytes[startIndex + 2] = 0;
            bytes[startIndex + 3] = 255;
        }

        /// <inheritdoc />
        public void ToZyxBytes(byte[] bytes, int startIndex)
        {
            Vector2 vector = this.ToVector2();
            vector /= 65534;
            vector *= 255;
            vector += Half;
            vector += Round;
            vector = Vector2.Clamp(vector, Vector2.Zero, MaxBytes);

            bytes[startIndex] = 0;
            bytes[startIndex + 1] = (byte)(float)Math.Round(vector.Y);
            bytes[startIndex + 2] = (byte)(float)Math.Round(vector.X);
        }

        /// <inheritdoc />
        public void ToZyxwBytes(byte[] bytes, int startIndex)
        {
            Vector2 vector = this.ToVector2();
            vector /= 65534;
            vector *= 255;
            vector += Half;
            vector += Round;
            vector = Vector2.Clamp(vector, Vector2.Zero, MaxBytes);

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
            return new Vector2((short)(this.PackedValue & 0xFFFF), (short)(this.PackedValue >> 0x10));
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return (obj is Short2) && this.Equals((Short2)obj);
        }

        /// <inheritdoc />
        public bool Equals(Short2 other)
        {
            return this == other;
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return this.PackedValue.GetHashCode();
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return this.PackedValue.ToString("x8");
        }

        /// <summary>
        /// Packs the <see cref="float"/> components into a <see cref="uint"/>.
        /// </summary>
        /// <param name="x">The x-component</param>
        /// <param name="y">The y-component</param>
        /// <returns>The <see cref="uint"/> containing the packed values.</returns>
        private static uint Pack(float x, float y)
        {
            // Largest two byte positive number 0xFFFF >> 1;
            const float MaxPos = 0x7FFF;
            const float MinNeg = ~(int)MaxPos;

            // Clamp the value between min and max values
            uint word2 = (uint)Math.Round(x.Clamp(MinNeg, MaxPos)) & 0xFFFF;
            uint word1 = ((uint)Math.Round(y.Clamp(MinNeg, MaxPos)) & 0xFFFF) << 0x10;

            return word2 | word1;
        }
    }
}