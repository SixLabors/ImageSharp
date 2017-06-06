// <copyright file="NormalizedByte2.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.PixelFormats
{
    using System;
    using System.Numerics;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Packed packed pixel type containing two 8-bit signed normalized values, ranging from −1 to 1.
    /// <para>
    /// Ranges from &lt;-1, -1, 0, 1&gt; to &lt;1, 1, 0, 1&gt; in vector form.
    /// </para>
    /// </summary>
    public struct NormalizedByte2 : IPixel<NormalizedByte2>, IPackedVector<ushort>
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
        /// Initializes a new instance of the <see cref="NormalizedByte2"/> struct.
        /// </summary>
        /// <param name="vector">The vector containing the component values.</param>
        public NormalizedByte2(Vector2 vector)
        {
            this.PackedValue = Pack(vector.X, vector.Y);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NormalizedByte2"/> struct.
        /// </summary>
        /// <param name="x">The x-component.</param>
        /// <param name="y">The y-component.</param>
        public NormalizedByte2(float x, float y)
        {
            this.PackedValue = Pack(x, y);
        }

        /// <inheritdoc/>
        public ushort PackedValue { get; set; }

        /// <summary>
        /// Compares two <see cref="NormalizedByte2"/> objects for equality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="NormalizedByte2"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="NormalizedByte2"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(NormalizedByte2 left, NormalizedByte2 right)
        {
            return left.PackedValue == right.PackedValue;
        }

        /// <summary>
        /// Compares two <see cref="NormalizedByte2"/> objects for equality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="NormalizedByte2"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="NormalizedByte2"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(NormalizedByte2 left, NormalizedByte2 right)
        {
            return left.PackedValue != right.PackedValue;
        }

        /// <inheritdoc />
        public PixelOperations<NormalizedByte2> CreatePixelOperations() => new PixelOperations<NormalizedByte2>();

        /// <summary>
        /// Expands the packed representation into a <see cref="Vector2"/>.
        /// The vector components are typically expanded in least to greatest significance order.
        /// </summary>
        /// <returns>The <see cref="Vector2"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector2 ToVector2()
        {
            return new Vector2(
                (sbyte)((this.PackedValue >> 0) & 0xFF) / 127F,
                (sbyte)((this.PackedValue >> 8) & 0xFF) / 127F);
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
            return new Vector4(this.ToVector2(), 0F, 1F);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromRgba32(Rgba32 source)
        {
            Vector4 vector = source.ToUnscaledVector4();
            vector -= Round;
            vector -= Half;
            vector -= Round;
            vector /= Half;
            this.PackFromVector4(vector);
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
        public override bool Equals(object obj)
        {
            return (obj is NormalizedByte2) && this.Equals((NormalizedByte2)obj);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(NormalizedByte2 other)
        {
            return this.PackedValue == other.PackedValue;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        /// Packs the <see cref="float"/> components into a <see cref="ushort"/>.
        /// </summary>
        /// <param name="x">The x-component</param>
        /// <param name="y">The y-component</param>
        /// <returns>The <see cref="ushort"/> containing the packed values.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ushort Pack(float x, float y)
        {
            int byte2 = ((ushort)Math.Round(x.Clamp(-1F, 1F) * 127F) & 0xFF) << 0;
            int byte1 = ((ushort)Math.Round(y.Clamp(-1F, 1F) * 127F) & 0xFF) << 8;

            return (ushort)(byte2 | byte1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Vector4 ToScaledVector4()
        {
            Vector4 vector = this.ToVector4();
            vector *= Half;
            vector += Round;
            vector += Half;
            vector += Round;
            vector = Vector4.Clamp(vector, Vector4.Zero, MaxBytes);
            return vector;
        }
    }
}