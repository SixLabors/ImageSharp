// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.PixelFormats
{
    /// <summary>
    /// Packed pixel type containing three 16-bit unsigned normalized values ranging from 0 to 635535.
    /// <para>
    /// Ranges from [0, 0, 0, 1] to [1, 1, 1, 1] in vector form.
    /// </para>
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct Rgb48 : IPixel<Rgb48>
    {
        private const float Max = 65535F;

        /// <summary>
        /// Gets or sets the red component.
        /// </summary>
        public ushort R;

        /// <summary>
        /// Gets or sets the green component.
        /// </summary>
        public ushort G;

        /// <summary>
        /// Gets or sets the blue component.
        /// </summary>
        public ushort B;

        /// <summary>
        /// Initializes a new instance of the <see cref="Rgb48"/> struct.
        /// </summary>
        /// <param name="r">The red component.</param>
        /// <param name="g">The green component.</param>
        /// <param name="b">The blue component.</param>
        public Rgb48(ushort r, ushort g, ushort b)
            : this()
        {
            this.R = r;
            this.G = g;
            this.B = b;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Rgb48"/> struct.
        /// </summary>
        /// <param name="r">The red component.</param>
        /// <param name="g">The green component.</param>
        /// <param name="b">The blue component.</param>
        public Rgb48(float r, float g, float b)
          : this()
        {
            this.R = (ushort)MathF.Round(r.Clamp(0, 1) * Max);
            this.G = (ushort)MathF.Round(g.Clamp(0, 1) * Max);
            this.B = (ushort)MathF.Round(b.Clamp(0, 1) * Max);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Rgb48"/> struct.
        /// </summary>
        /// <param name="vector">The vector containing the components values.</param>
        public Rgb48(Vector3 vector)
            : this(vector.X, vector.Y, vector.Z)
        {
        }

        /// <summary>
        /// Compares two <see cref="Rgb48"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="Rgb48"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="Rgb48"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(Rgb48 left, Rgb48 right)
        {
            return left.R == right.R
                && left.G == right.G
                && left.B == right.B;
        }

        /// <summary>
        /// Compares two <see cref="Rgb48"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="Rgb48"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="Rgb48"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(Rgb48 left, Rgb48 right)
        {
            return left.R != right.R
                || left.G != right.G
                || left.B != right.B;
        }

        /// <inheritdoc />
        public PixelOperations<Rgb48> CreatePixelOperations() => new PixelOperations<Rgb48>();

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromScaledVector4(Vector4 vector)
        {
            this.PackFromVector4(vector);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector4 ToScaledVector4()
        {
            return this.ToVector4();
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector4 ToVector4()
        {
            return new Vector4(this.R / Max, this.G / Max, this.B / Max, 1);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromVector4(Vector4 vector)
        {
            vector = Vector4.Clamp(vector, Vector4.Zero, Vector4.One) * Max;
            this.R = (ushort)MathF.Round(vector.X);
            this.G = (ushort)MathF.Round(vector.Y);
            this.B = (ushort)MathF.Round(vector.Z);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromRgba32(Rgba32 source)
        {
            this.PackFromVector4(source.ToVector4());
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromArgb32(Argb32 source)
        {
            this.PackFromVector4(source.ToVector4());
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromBgra32(Bgra32 source)
        {
            this.PackFromVector4(source.ToVector4());
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromRgba64(Rgba64 source) => this = source.Rgb;

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToRgb24(ref Rgb24 dest)
        {
            dest.R = (byte)(((this.R * 255) + 32895) >> 16);
            dest.G = (byte)(((this.G * 255) + 32895) >> 16);
            dest.B = (byte)(((this.B * 255) + 32895) >> 16);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToRgba32(ref Rgba32 dest)
        {
            dest.R = (byte)(((this.R * 255) + 32895) >> 16);
            dest.G = (byte)(((this.G * 255) + 32895) >> 16);
            dest.B = (byte)(((this.B * 255) + 32895) >> 16);
            dest.A = 255;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToArgb32(ref Argb32 dest)
        {
            dest.R = (byte)(((this.R * 255) + 32895) >> 16);
            dest.G = (byte)(((this.G * 255) + 32895) >> 16);
            dest.B = (byte)(((this.B * 255) + 32895) >> 16);
            dest.A = 255;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToBgr24(ref Bgr24 dest)
        {
            dest.R = (byte)(((this.R * 255) + 32895) >> 16);
            dest.G = (byte)(((this.G * 255) + 32895) >> 16);
            dest.B = (byte)(((this.B * 255) + 32895) >> 16);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToBgra32(ref Bgra32 dest)
        {
            dest.R = (byte)(((this.R * 255) + 32895) >> 16);
            dest.G = (byte)(((this.G * 255) + 32895) >> 16);
            dest.B = (byte)(((this.B * 255) + 32895) >> 16);
            dest.A = 255;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromRgb48(Rgb48 source) => this = source;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToRgb48(ref Rgb48 dest) => dest = this;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToRgba64(ref Rgba64 dest)
        {
            dest.Rgb = this;
            dest.A = ushort.MaxValue;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is Rgb48 rgb48 && this.Equals(rgb48);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(Rgb48 other)
        {
            return this.R == other.R
                && this.G == other.G
                && this.B == other.B;
        }

        /// <inheritdoc />
        public override string ToString() => this.ToVector4().ToString();

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            return HashHelpers.Combine(
                this.R.GetHashCode(),
                HashHelpers.Combine(this.G.GetHashCode(), this.B.GetHashCode()));
        }
    }
}