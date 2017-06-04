// <copyright file="Rgba64.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.PixelFormats
{
    using System;
    using System.Numerics;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Packed pixel type containing four 16-bit unsigned normalized values ranging from 0 to 1.
    /// <para>
    /// Ranges from &lt;0, 0, 0, 0&gt; to &lt;1, 1, 1, 1&gt; in vector form.
    /// </para>
    /// </summary>
    public struct Rgba64 : IPixel<Rgba64>, IPackedVector<ulong>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Rgba64"/> struct.
        /// </summary>
        /// <param name="x">The x-component</param>
        /// <param name="y">The y-component</param>
        /// <param name="z">The z-component</param>
        /// <param name="w">The w-component</param>
        public Rgba64(float x, float y, float z, float w)
        {
            this.PackedValue = Pack(x, y, z, w);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Rgba64"/> struct.
        /// </summary>
        /// <param name="vector">The vector containing the components values.</param>
        public Rgba64(Vector4 vector)
        {
            this.PackedValue = Pack(vector.X, vector.Y, vector.Z, vector.W);
        }

        /// <inheritdoc/>
        public ulong PackedValue { get; set; }

        /// <summary>
        /// Compares two <see cref="Rgba64"/> objects for equality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="Rgba64"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="Rgba64"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(Rgba64 left, Rgba64 right)
        {
            return left.PackedValue == right.PackedValue;
        }

        /// <summary>
        /// Compares two <see cref="Rgba64"/> objects for equality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="Rgba64"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="Rgba64"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(Rgba64 left, Rgba64 right)
        {
            return left.PackedValue != right.PackedValue;
        }

        /// <inheritdoc />
        public PixelOperations<Rgba64> CreatePixelOperations() => new PixelOperations<Rgba64>();

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector4 ToVector4()
        {
            return new Vector4(
                (this.PackedValue & 0xFFFF) / 65535F,
                ((this.PackedValue >> 16) & 0xFFFF) / 65535F,
                ((this.PackedValue >> 32) & 0xFFFF) / 65535F,
                ((this.PackedValue >> 48) & 0xFFFF) / 65535F);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromVector4(Vector4 vector)
        {
            this.PackedValue = Pack(vector.X, vector.Y, vector.Z, vector.W);
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
            Vector4 vector = this.ToVector4() * 255F;
            dest.R = (byte)MathF.Round(vector.X);
            dest.G = (byte)MathF.Round(vector.Y);
            dest.B = (byte)MathF.Round(vector.Z);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToRgba32(ref Rgba32 dest)
        {
            Vector4 vector = this.ToVector4() * 255F;
            dest.R = (byte)MathF.Round(vector.X);
            dest.G = (byte)MathF.Round(vector.Y);
            dest.B = (byte)MathF.Round(vector.Z);
            dest.A = (byte)MathF.Round(vector.W);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToBgr24(ref Bgr24 dest)
        {
            Vector4 vector = this.ToVector4() * 255F;
            dest.R = (byte)MathF.Round(vector.X);
            dest.G = (byte)MathF.Round(vector.Y);
            dest.B = (byte)MathF.Round(vector.Z);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToBgra32(ref Bgra32 dest)
        {
            Vector4 vector = this.ToVector4() * 255F;
            dest.R = (byte)MathF.Round(vector.X);
            dest.G = (byte)MathF.Round(vector.Y);
            dest.B = (byte)MathF.Round(vector.Z);
            dest.A = (byte)MathF.Round(vector.W);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return (obj is Rgba64) && this.Equals((Rgba64)obj);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(Rgba64 other)
        {
            return this.PackedValue == other.PackedValue;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return this.ToVector4().ToString();
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            return this.PackedValue.GetHashCode();
        }

        /// <summary>
        /// Packs the <see cref="float"/> components into a <see cref="ulong"/>.
        /// </summary>
        /// <param name="x">The x-component</param>
        /// <param name="y">The y-component</param>
        /// <param name="z">The z-component</param>
        /// <param name="w">The w-component</param>
        /// <returns>The <see cref="ulong"/> containing the packed values.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong Pack(float x, float y, float z, float w)
        {
            return (ulong)Math.Round(x.Clamp(0, 1) * 65535F) |
                   ((ulong)Math.Round(y.Clamp(0, 1) * 65535F) << 16) |
                   ((ulong)Math.Round(z.Clamp(0, 1) * 65535F) << 32) |
                   ((ulong)Math.Round(w.Clamp(0, 1) * 65535F) << 48);
        }
    }
}
