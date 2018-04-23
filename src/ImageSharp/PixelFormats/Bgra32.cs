// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.PixelFormats
{
    /// <summary>
    /// Packed pixel type containing four 8-bit unsigned normalized values ranging from 0 to 255.
    /// The color components are stored in blue, green, red, and alpha order (least significant to most significant byte).
    /// <para>
    /// Ranges from [0, 0, 0, 0] to [1, 1, 1, 1] in vector form.
    /// </para>
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct Bgra32 : IPixel<Bgra32>, IPackedVector<uint>
    {
        /// <summary>
        /// Gets or sets the blue component.
        /// </summary>
        public byte B;

        /// <summary>
        /// Gets or sets the green component.
        /// </summary>
        public byte G;

        /// <summary>
        /// Gets or sets the red component.
        /// </summary>
        public byte R;

        /// <summary>
        /// Gets or sets the alpha component.
        /// </summary>
        public byte A;

        /// <summary>
        /// Initializes a new instance of the <see cref="Bgra32"/> struct.
        /// </summary>
        /// <param name="r">The red component.</param>
        /// <param name="g">The green component.</param>
        /// <param name="b">The blue component.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Bgra32(byte r, byte g, byte b)
        {
            this.R = r;
            this.G = g;
            this.B = b;
            this.A = 255;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Bgra32"/> struct.
        /// </summary>
        /// <param name="r">The red component.</param>
        /// <param name="g">The green component.</param>
        /// <param name="b">The blue component.</param>
        /// <param name="a">The alpha component.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Bgra32(byte r, byte g, byte b, byte a)
        {
            this.R = r;
            this.G = g;
            this.B = b;
            this.A = a;
        }

        /// <summary>
        /// Gets or sets the packed representation of the Bgra32 struct.
        /// </summary>
        public uint Bgra
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Unsafe.As<Bgra32, uint>(ref this);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => Unsafe.As<Bgra32, uint>(ref this) = value;
        }

        /// <inheritdoc/>
        public uint PackedValue
        {
            get => this.Bgra;
            set => this.Bgra = value;
        }

        /// <inheritdoc/>
        public PixelOperations<Bgra32> CreatePixelOperations() => new PixelOperations<Bgra32>();

        /// <inheritdoc/>
        public bool Equals(Bgra32 other)
        {
            return this.R == other.R && this.G == other.G && this.B == other.B && this.A == other.A;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is Bgra32 other && this.Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = this.B;
                hashCode = (hashCode * 397) ^ this.G;
                hashCode = (hashCode * 397) ^ this.R;
                hashCode = (hashCode * 397) ^ this.A;
                return hashCode;
            }
        }

        /// <summary>
        /// Gets the <see cref="Vector4"/> representation without normalizing to [0, 1]
        /// </summary>
        /// <returns>A <see cref="Vector4"/> of values in [0, 255] </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Vector4 ToByteScaledVector4()
        {
            return new Vector4(this.R, this.G, this.B, this.A);
        }

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

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromVector4(Vector4 vector)
        {
            var rgba = default(Rgba32);
            rgba.PackFromVector4(vector);
            this.PackFromRgba32(rgba);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector4 ToVector4()
        {
            return this.ToRgba32().ToVector4();
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromRgba32(Rgba32 source)
        {
            this.R = source.R;
            this.G = source.G;
            this.B = source.B;
            this.A = source.A;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromArgb32(Argb32 source)
        {
            this.R = source.R;
            this.G = source.G;
            this.B = source.B;
            this.A = source.A;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromBgra32(Bgra32 source)
        {
            this.PackedValue = source.PackedValue;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToRgb24(ref Rgb24 dest)
        {
            dest.R = this.R;
            dest.G = this.G;
            dest.B = this.B;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToRgba32(ref Rgba32 dest)
        {
            dest.R = this.R;
            dest.G = this.G;
            dest.B = this.B;
            dest.A = this.A;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToArgb32(ref Argb32 dest)
        {
            dest.R = this.R;
            dest.G = this.G;
            dest.B = this.B;
            dest.A = this.A;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToBgr24(ref Bgr24 dest)
        {
            dest = Unsafe.As<Bgra32, Bgr24>(ref this);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToBgra32(ref Bgra32 dest)
        {
            dest = this;
        }

        /// <summary>
        /// Converts the pixel to <see cref="Rgba32"/> format.
        /// </summary>
        /// <returns>The RGBA value</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Rgba32 ToRgba32() => new Rgba32(this.R, this.G, this.B, this.A);

        /// <summary>
        /// Converts the pixel to <see cref="Argb32"/> format.
        /// </summary>
        /// <returns>The RGBA value</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Argb32 ToArgb32() => new Argb32(this.R, this.G, this.B, this.A);

        /// <summary>
        /// Converts the pixel to <see cref="Bgra32"/> format.
        /// </summary>
        /// <returns>The RGBA value</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Bgra32 ToBgra32() => this;
    }
}