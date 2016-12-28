// <copyright file="HalfSingle.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Numerics;

    /// <summary>
    /// Packed pixel type containing a single 16 bit floating point value.
    /// </summary>
    public struct HalfSingle : IPackedPixel<ushort>, IEquatable<HalfSingle>
    {
        /// <summary>
        /// The maximum byte value.
        /// </summary>
        private static readonly Vector4 MaxBytes = new Vector4(255);

        /// <summary>
        /// The half vector value.
        /// </summary>
        private static readonly Vector4 Half = new Vector4(0.5F);

        /// <summary>
        /// Initializes a new instance of the <see cref="HalfSingle"/> struct.
        /// </summary>
        /// <param name="single">The single component.</param>
        public HalfSingle(float single)
        {
            this.PackedValue = HalfTypeHelper.Pack(single);
        }

        /// <inheritdoc />
        public ushort PackedValue { get; set; }

        /// <summary>
        /// Compares two <see cref="HalfSingle"/> objects for equality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="HalfSingle"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="HalfSingle"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator ==(HalfSingle left, HalfSingle right)
        {
            return left.PackedValue == right.PackedValue;
        }

        /// <summary>
        /// Compares two <see cref="HalfSingle"/> objects for equality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="HalfSingle"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="HalfSingle"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator !=(HalfSingle left, HalfSingle right)
        {
            return left.PackedValue != right.PackedValue;
        }

        /// <summary>
        /// Expands the packed representation into a <see cref="float"/>.
        /// </summary>
        /// <returns>The <see cref="float"/>.</returns>
        public float ToSingle()
        {
            return HalfTypeHelper.Unpack(this.PackedValue);
        }

        /// <inheritdoc />
        public void PackFromVector4(Vector4 vector)
        {
            this.PackedValue = HalfTypeHelper.Pack(vector.X);
        }

        /// <inheritdoc />
        public Vector4 ToVector4()
        {
            return new Vector4(this.ToSingle(), 0, 0, 1);
        }

        /// <inheritdoc />
        public void PackFromBytes(byte x, byte y, byte z, byte w)
        {
            this.PackFromVector4(new Vector4(x, y, z, w) / MaxBytes);
        }

        /// <inheritdoc />
        public void ToXyzBytes(byte[] bytes, int startIndex)
        {
            Vector4 vector = this.ToVector4();
            vector *= MaxBytes;
            vector += Half;
            vector = Vector4.Clamp(vector, Vector4.Zero, MaxBytes);

            bytes[startIndex] = (byte)vector.X;
            bytes[startIndex + 1] = (byte)vector.Y;
            bytes[startIndex + 2] = (byte)vector.Z;
        }

        /// <inheritdoc />
        public void ToXyzwBytes(byte[] bytes, int startIndex)
        {
            Vector4 vector = this.ToVector4();
            vector *= MaxBytes;
            vector += Half;
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
            vector *= MaxBytes;
            vector += Half;
            vector = Vector4.Clamp(vector, Vector4.Zero, MaxBytes);

            bytes[startIndex] = (byte)vector.Z;
            bytes[startIndex + 1] = (byte)vector.Y;
            bytes[startIndex + 2] = (byte)vector.X;
        }

        /// <inheritdoc />
        public void ToZyxwBytes(byte[] bytes, int startIndex)
        {
            Vector4 vector = this.ToVector4();
            vector *= MaxBytes;
            vector += Half;
            vector = Vector4.Clamp(vector, Vector4.Zero, MaxBytes);

            bytes[startIndex] = (byte)vector.Z;
            bytes[startIndex + 1] = (byte)vector.Y;
            bytes[startIndex + 2] = (byte)vector.X;
            bytes[startIndex + 3] = (byte)vector.W;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return (obj is HalfSingle) && this.Equals((HalfSingle)obj);
        }

        /// <inheritdoc />
        public bool Equals(HalfSingle other)
        {
            return this.PackedValue == other.PackedValue;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return this.ToSingle().ToString();
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return this.PackedValue.GetHashCode();
        }
    }
}