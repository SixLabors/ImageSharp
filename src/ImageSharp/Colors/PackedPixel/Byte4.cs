// <copyright file="Byte4.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Numerics;

    /// <summary>
    /// Packed pixel type containing four 8-bit unsigned integer values, ranging from 0 to 255.
    /// </summary>
    public struct Byte4 : IPackedPixel<uint>, IEquatable<Byte4>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Byte4"/> struct.
        /// </summary>
        /// <param name="vector">
        /// A vector containing the initial values for the components of the Byte4 structure.
        /// </param>
        public Byte4(Vector4 vector)
        {
            this.PackedValue = Pack(ref vector);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Byte4"/> struct.
        /// </summary>
        /// <param name="x">The x-component</param>
        /// <param name="y">The y-component</param>
        /// <param name="z">The z-component</param>
        /// <param name="w">The w-component</param>
        public Byte4(float x, float y, float z, float w)
        {
            Vector4 vector = new Vector4(x, y, z, w);
            this.PackedValue = Pack(ref vector);
        }

        /// <inheritdoc />
        public uint PackedValue { get; set; }

        /// <summary>
        /// Compares two <see cref="Byte4"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="Byte4"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="Byte4"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator ==(Byte4 left, Byte4 right)
        {
            return left.PackedValue == right.PackedValue;
        }

        /// <summary>
        /// Compares two <see cref="Byte4"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="Byte4"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="Byte4"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator !=(Byte4 left, Byte4 right)
        {
            return left.PackedValue != right.PackedValue;
        }

        /// <summary>
        /// Sets the packed representation from a Vector4.
        /// </summary>
        /// <param name="vector">The vector to create the packed representation from.</param>
        public void PackFromVector4(Vector4 vector)
        {
            this.PackedValue = Pack(ref vector);
        }

        /// <summary>
        /// Expands the packed representation into a Vector4.
        /// </summary>
        /// <returns>The expanded vector.</returns>
        public Vector4 ToVector4()
        {
            return new Vector4(
                this.PackedValue & 0xFF,
                (this.PackedValue >> 0x8) & 0xFF,
                (this.PackedValue >> 0x10) & 0xFF,
                (this.PackedValue >> 0x18) & 0xFF);
        }

        /// <inheritdoc />
        public void PackFromBytes(byte x, byte y, byte z, byte w)
        {
            this.PackFromVector4(new Vector4(x, y, z, w));
        }

        /// <inheritdoc />
        public void ToXyzBytes(byte[] bytes, int startIndex)
        {
            Vector4 vector = this.ToVector4();
            bytes[startIndex] = (byte)vector.X;
            bytes[startIndex + 1] = (byte)vector.Y;
            bytes[startIndex + 2] = (byte)vector.Z;
        }

        /// <inheritdoc />
        public void ToXyzwBytes(byte[] bytes, int startIndex)
        {
            Vector4 vector = this.ToVector4();
            bytes[startIndex] = (byte)vector.X;
            bytes[startIndex + 1] = (byte)vector.Y;
            bytes[startIndex + 2] = (byte)vector.Z;
            bytes[startIndex + 3] = (byte)vector.W;
        }

        /// <inheritdoc />
        public void ToZyxBytes(byte[] bytes, int startIndex)
        {
            Vector4 vector = this.ToVector4();
            bytes[startIndex] = (byte)vector.Z;
            bytes[startIndex + 1] = (byte)vector.Y;
            bytes[startIndex + 2] = (byte)vector.X;
        }

        /// <inheritdoc />
        public void ToZyxwBytes(byte[] bytes, int startIndex)
        {
            Vector4 vector = this.ToVector4();
            bytes[startIndex] = (byte)vector.Z;
            bytes[startIndex + 1] = (byte)vector.Y;
            bytes[startIndex + 2] = (byte)vector.X;
            bytes[startIndex + 3] = (byte)vector.W;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return (obj is Byte4) && this.Equals((Byte4)obj);
        }

        /// <inheritdoc />
        public bool Equals(Byte4 other)
        {
            return this == other;
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return this.PackedValue.GetHashCode();
        }

        /// <summary>
        /// Returns a string representation of the current instance.
        /// </summary>
        /// <returns>String that represents the object.</returns>
        public override string ToString()
        {
            return this.PackedValue.ToString("x8");
        }

        /// <summary>
        /// Packs a vector into a uint.
        /// </summary>
        /// <param name="vector">The vector containing the values to pack.</param>
        /// <returns>The <see cref="uint"/> containing the packed values.</returns>
        private static uint Pack(ref Vector4 vector)
        {
            const float Max = 255F;
            const float Min = 0F;

            // Clamp the value between min and max values
            uint byte4 = (uint)Math.Round(vector.X.Clamp(Min, Max)) & 0xFF;
            uint byte3 = ((uint)Math.Round(vector.Y.Clamp(Min, Max)) & 0xFF) << 0x8;
            uint byte2 = ((uint)Math.Round(vector.Z.Clamp(Min, Max)) & 0xFF) << 0x10;
            uint byte1 = ((uint)Math.Round(vector.W.Clamp(Min, Max)) & 0xFF) << 0x18;

            return byte4 | byte3 | byte2 | byte1;
        }
    }
}