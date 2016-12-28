// <copyright file="NormalizedByte4.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Numerics;

    /// <summary>
    /// Packed pixel type containing four 8-bit signed normalized values, ranging from −1 to 1.
    /// </summary>
    public struct NormalizedByte4 : IPackedPixel<uint>, IEquatable<NormalizedByte4>
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
        /// Initializes a new instance of the <see cref="NormalizedByte4"/> struct.
        /// </summary>
        /// <param name="vector">The vector containing the component values.</param>
        public NormalizedByte4(Vector4 vector)
        {
            this.PackedValue = Pack(vector.X, vector.Y, vector.Z, vector.W);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NormalizedByte4"/> struct.
        /// </summary>
        /// <param name="x">The x-component.</param>
        /// <param name="y">The y-component.</param>
        /// <param name="z">The z-component.</param>
        /// <param name="w">The w-component.</param>
        public NormalizedByte4(float x, float y, float z, float w)
        {
            this.PackedValue = Pack(x, y, z, w);
        }

        /// <inheritdoc />
        public uint PackedValue { get; set; }

        /// <summary>
        /// Compares two <see cref="NormalizedByte4"/> objects for equality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="NormalizedByte4"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="NormalizedByte4"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator ==(NormalizedByte4 left, NormalizedByte4 right)
        {
            return left.PackedValue == right.PackedValue;
        }

        /// <summary>
        /// Compares two <see cref="NormalizedByte4"/> objects for equality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="NormalizedByte4"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="NormalizedByte4"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator !=(NormalizedByte4 left, NormalizedByte4 right)
        {
            return left.PackedValue != right.PackedValue;
        }

        /// <inheritdoc />
        public void PackFromVector4(Vector4 vector)
        {
            this.PackedValue = Pack(vector.X, vector.Y, vector.Z, vector.W);
        }

        /// <inheritdoc />
        public Vector4 ToVector4()
        {
            return new Vector4(
                (sbyte)((this.PackedValue >> 0) & 0xFF) / 127F,
                (sbyte)((this.PackedValue >> 8) & 0xFF) / 127F,
                (sbyte)((this.PackedValue >> 16) & 0xFF) / 127F,
                (sbyte)((this.PackedValue >> 24) & 0xFF) / 127F);
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

            bytes[startIndex] = (byte)vector.X;
            bytes[startIndex + 1] = (byte)vector.Y;
            bytes[startIndex + 2] = (byte)vector.Z;
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

            bytes[startIndex] = (byte)vector.X;
            bytes[startIndex + 1] = (byte)vector.Y;
            bytes[startIndex + 2] = (byte)vector.Z;
            bytes[startIndex + 3] = (byte)vector.W;
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

            bytes[startIndex] = (byte)vector.Z;
            bytes[startIndex + 1] = (byte)vector.Y;
            bytes[startIndex + 2] = (byte)vector.X;
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

            bytes[startIndex] = (byte)vector.Z;
            bytes[startIndex + 1] = (byte)vector.Y;
            bytes[startIndex + 2] = (byte)vector.X;
            bytes[startIndex + 3] = (byte)vector.W;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return (obj is NormalizedByte4) && this.Equals((NormalizedByte4)obj);
        }

        /// <inheritdoc />
        public bool Equals(NormalizedByte4 other)
        {
            return this.PackedValue == other.PackedValue;
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
        /// <param name="z">The z-component</param>
        /// <param name="w">The w-component</param>
        /// <returns>The <see cref="uint"/> containing the packed values.</returns>
        private static uint Pack(float x, float y, float z, float w)
        {
            uint byte4 = ((uint)Math.Round(x.Clamp(-1F, 1F) * 127F) & 0xFF) << 0;
            uint byte3 = ((uint)Math.Round(y.Clamp(-1F, 1F) * 127F) & 0xFF) << 8;
            uint byte2 = ((uint)Math.Round(z.Clamp(-1F, 1F) * 127F) & 0xFF) << 16;
            uint byte1 = ((uint)Math.Round(w.Clamp(-1F, 1F) * 127F) & 0xFF) << 24;

            return byte4 | byte3 | byte2 | byte1;
        }
    }
}