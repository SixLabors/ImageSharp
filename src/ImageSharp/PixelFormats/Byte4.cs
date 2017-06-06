// <copyright file="Byte4.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.PixelFormats
{
    using System;
    using System.Numerics;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Packed pixel type containing four 8-bit unsigned integer values, ranging from 0 to 255.
    /// <para>
    /// Ranges from &lt;0, 0, 0, 0&gt; to &lt;1, 1, 1, 1&gt; in vector form.
    /// </para>
    /// </summary>
    public struct Byte4 : IPixel<Byte4>, IPackedVector<uint>
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

        /// <inheritdoc/>
        public uint PackedValue { get; set; }

        /// <summary>
        /// Compares two <see cref="Byte4"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="Byte4"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="Byte4"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(Byte4 left, Byte4 right)
        {
            return left.PackedValue != right.PackedValue;
        }

        /// <inheritdoc />
        public PixelOperations<Byte4> CreatePixelOperations() => new PixelOperations<Byte4>();

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromVector4(Vector4 vector)
        {
            this.PackedValue = Pack(ref vector);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector4 ToVector4()
        {
            return new Vector4(
                this.PackedValue & 0xFF,
                (this.PackedValue >> 0x8) & 0xFF,
                (this.PackedValue >> 0x10) & 0xFF,
                (this.PackedValue >> 0x18) & 0xFF);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromRgba32(Rgba32 source)
        {
            this.PackFromVector4(source.ToUnscaledVector4());
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToRgb24(ref Rgb24 dest)
        {
            Vector4 vector = this.ToVector4();
            dest.R = (byte)vector.X;
            dest.G = (byte)vector.Y;
            dest.B = (byte)vector.Z;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToRgba32(ref Rgba32 dest)
        {
            Vector4 vector = this.ToVector4();
            dest.R = (byte)vector.X;
            dest.G = (byte)vector.Y;
            dest.B = (byte)vector.Z;
            dest.A = (byte)vector.W;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToBgr24(ref Bgr24 dest)
        {
            Vector4 vector = this.ToVector4();
            dest.R = (byte)vector.X;
            dest.G = (byte)vector.Y;
            dest.B = (byte)vector.Z;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToBgra32(ref Bgra32 dest)
        {
            Vector4 vector = this.ToVector4();
            dest.R = (byte)vector.X;
            dest.G = (byte)vector.Y;
            dest.B = (byte)vector.Z;
            dest.A = (byte)vector.W;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return (obj is Byte4) && this.Equals((Byte4)obj);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(Byte4 other)
        {
            return this == other;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Pack(ref Vector4 vector)
        {
            const float Max = 255F;
            const float Min = 0F;

            // Clamp the value between min and max values
            // TODO: Use Vector4.Clamp() here!
            uint byte4 = (uint)Math.Round(vector.X.Clamp(Min, Max)) & 0xFF;
            uint byte3 = ((uint)Math.Round(vector.Y.Clamp(Min, Max)) & 0xFF) << 0x8;
            uint byte2 = ((uint)Math.Round(vector.Z.Clamp(Min, Max)) & 0xFF) << 0x10;
            uint byte1 = ((uint)Math.Round(vector.W.Clamp(Min, Max)) & 0xFF) << 0x18;

            return byte4 | byte3 | byte2 | byte1;
        }
    }
}