// <copyright file="HalfVector2.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.PixelFormats
{
    using System;
    using System.Numerics;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Packed pixel type containing two 16-bit floating-point values.
    /// <para>
    /// Ranges from &lt;0, 0, 0, 1&gt; to &lt;1, 0, 0, 1&gt; in vector form.
    /// </para>
    /// </summary>
    public struct HalfVector2 : IPixel<HalfVector2>, IPackedVector<uint>
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
        /// Initializes a new instance of the <see cref="HalfVector2"/> struct.
        /// </summary>
        /// <param name="x">The x-component.</param>
        /// <param name="y">The y-component.</param>
        public HalfVector2(float x, float y)
        {
            this.PackedValue = Pack(x, y);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HalfVector2"/> struct.
        /// </summary>
        /// <param name="vector">A vector containing the initial values for the components.</param>
        public HalfVector2(Vector2 vector)
        {
            this.PackedValue = Pack(vector.X, vector.Y);
        }

        /// <inheritdoc/>
        public uint PackedValue { get; set; }

        /// <summary>
        /// Compares two <see cref="HalfVector2"/> objects for equality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="HalfVector2"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="HalfVector2"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(HalfVector2 left, HalfVector2 right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Compares two <see cref="HalfVector2"/> objects for equality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="HalfVector2"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="HalfVector2"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(HalfVector2 left, HalfVector2 right)
        {
            return !left.Equals(right);
        }

        /// <inheritdoc />
        public PixelOperations<HalfVector2> CreatePixelOperations() => new PixelOperations<HalfVector2>();

        /// <summary>
        /// Expands the packed representation into a <see cref="Vector2"/>.
        /// </summary>
        /// <returns>The <see cref="Vector2"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector2 ToVector2()
        {
            Vector2 vector;
            vector.X = HalfTypeHelper.Unpack((ushort)this.PackedValue);
            vector.Y = HalfTypeHelper.Unpack((ushort)(this.PackedValue >> 0x10));
            return vector;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromVector4(Vector4 vector)
        {
            this.PackedValue = Pack(vector.X, vector.Y);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector4 ToVector4()
        {
            Vector2 vector = this.ToVector2();
            return new Vector4(vector.X, vector.Y, 0F, 1F);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromRgba32(Rgba32 source)
        {
            this.PackFromVector4(source.ToVector4());
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToRgb24(ref Rgb24 dest)
        {
            Vector4 vector = this.ToScaledVector4();
            dest.R = (byte)vector.X;
            dest.G = (byte)vector.Y;
            dest.B = 0;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToRgba32(ref Rgba32 dest)
        {
            Vector4 vector = this.ToScaledVector4();
            dest.R = (byte)vector.X;
            dest.G = (byte)vector.Y;
            dest.B = 0;
            dest.A = 255;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToBgr24(ref Bgr24 dest)
        {
            Vector4 vector = this.ToScaledVector4();
            dest.R = (byte)vector.X;
            dest.G = (byte)vector.Y;
            dest.B = 0;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToBgra32(ref Bgra32 dest)
        {
            Vector4 vector = this.ToScaledVector4();
            dest.R = (byte)vector.X;
            dest.G = (byte)vector.Y;
            dest.B = 0;
            dest.A = 255;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return this.ToVector2().ToString();
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            return this.PackedValue.GetHashCode();
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return (obj is HalfVector2) && this.Equals((HalfVector2)obj);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(HalfVector2 other)
        {
            return this.PackedValue.Equals(other.PackedValue);
        }

        /// <summary>
        /// Packs the <see cref="float"/> components into a <see cref="uint"/>.
        /// </summary>
        /// <param name="x">The x-component</param>
        /// <param name="y">The y-component</param>
        /// <returns>The <see cref="uint"/> containing the packed values.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Pack(float x, float y)
        {
            uint num2 = HalfTypeHelper.Pack(x);
            uint num = (uint)(HalfTypeHelper.Pack(y) << 0x10);
            return num2 | num;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Vector4 ToScaledVector4()
        {
            Vector4 vector = this.ToVector4();
            vector *= MaxBytes;
            vector += Half;
            vector = Vector4.Clamp(vector, Vector4.Zero, MaxBytes);
            return vector;
        }
    }
}