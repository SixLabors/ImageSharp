// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.PixelFormats
{
    /// <summary>
    /// Packed pixel type containing four 16-bit unsigned normalized values ranging from 0 to 635535.
    /// <para>
    /// Ranges from [0, 0, 0, 0] to [1, 1, 1, 1] in vector form.
    /// </para>
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct Rgba64 : IPixel<Rgba64>, IPackedVector<ulong>
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
        /// Gets or sets the alpha component.
        /// </summary>
        public ushort A;

        /// <summary>
        /// Initializes a new instance of the <see cref="Rgba64"/> struct.
        /// </summary>
        /// <param name="r">The red component.</param>
        /// <param name="g">The green component.</param>
        /// <param name="b">The blue component.</param>
        /// <param name="a">The alpha component.</param>
        public Rgba64(ushort r, ushort g, ushort b, ushort a)
            : this()
        {
            this.R = r;
            this.G = g;
            this.B = b;
            this.A = a;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Rgba64"/> struct.
        /// </summary>
        /// <param name="r">The red component.</param>
        /// <param name="g">The green component.</param>
        /// <param name="b">The blue component.</param>
        /// <param name="a">The alpha component.</param>
        public Rgba64(float r, float g, float b, float a)
          : this()
        {
            this.R = (ushort)MathF.Round(r.Clamp(0, 1) * Max);
            this.G = (ushort)MathF.Round(g.Clamp(0, 1) * Max);
            this.B = (ushort)MathF.Round(b.Clamp(0, 1) * Max);
            this.A = (ushort)MathF.Round(a.Clamp(0, 1) * Max);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Rgba64"/> struct.
        /// </summary>
        /// <param name="vector">The vector containing the components values.</param>
        public Rgba64(Vector4 vector)
            : this(vector.X, vector.Y, vector.Z, vector.W)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Rgba64"/> struct.
        /// </summary>
        /// <param name="packed">The packed value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Rgba64(ulong packed)
            : this()
        {
            this.PackedValue = packed;
        }

        /// <summary>
        /// Gets or sets the RGB components of this struct as <see cref="Rgb48"/>
        /// </summary>
        public Rgb48 Rgb
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Unsafe.As<Rgba64, Rgb48>(ref this);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => Unsafe.As<Rgba64, Rgb48>(ref this) = value;
        }

        /// <inheritdoc/>
        public ulong PackedValue
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Unsafe.As<Rgba64, ulong>(ref this);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => Unsafe.As<Rgba64, ulong>(ref this) = value;
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
            return new Vector4(this.R, this.G, this.B, this.A) / Max;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromVector4(Vector4 vector)
        {
            vector = Vector4.Clamp(vector, Vector4.Zero, Vector4.One) * Max;
            this.R = (ushort)MathF.Round(vector.X);
            this.G = (ushort)MathF.Round(vector.Y);
            this.B = (ushort)MathF.Round(vector.Z);
            this.A = (ushort)MathF.Round(vector.W);
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
        public void PackFromRgba64(Rgba64 source) => this = source;

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
            // Taken from libpng pngtran.c line: 2419
            dest.R = (byte)(((this.R * 255) + 32895) >> 16);
            dest.G = (byte)(((this.G * 255) + 32895) >> 16);
            dest.B = (byte)(((this.B * 255) + 32895) >> 16);
            dest.A = (byte)(((this.A * 255) + 32895) >> 16);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromRgb48(Rgb48 source)
        {
            this.Rgb = source;
            this.A = ushort.MaxValue;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToRgb48(ref Rgb48 dest) => dest = this.Rgb;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToRgba64(ref Rgba64 dest) => dest = this;

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToArgb32(ref Argb32 dest)
        {
            dest.R = (byte)(((this.R * 255) + 32895) >> 16);
            dest.G = (byte)(((this.G * 255) + 32895) >> 16);
            dest.B = (byte)(((this.B * 255) + 32895) >> 16);
            dest.A = (byte)(((this.A * 255) + 32895) >> 16);
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
            dest.A = (byte)(((this.A * 255) + 32895) >> 16);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is Rgba64 rgba64 && this.Equals(rgba64);
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
            return $"({this.R},{this.G},{this.B},{this.A})";
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode() => this.PackedValue.GetHashCode();
    }
}