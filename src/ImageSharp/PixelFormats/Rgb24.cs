// <copyright file="Rgb24.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.PixelFormats
{
    using System;
    using System.Numerics;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Pixel type containing three 8-bit unsigned normalized values ranging from 0 to 255.
    /// The color components are stored in red, green, blue order.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct Rgb24 : IPixel<Rgb24>
    {
        /// <summary>
        /// The red component.
        /// </summary>
        public byte R;

        /// <summary>
        /// The green component.
        /// </summary>
        public byte G;

        /// <summary>
        /// The blue component.
        /// </summary>
        public byte B;

        /// <summary>
        /// Initializes a new instance of the <see cref="Rgb24"/> struct.
        /// </summary>
        /// <param name="r">The red component.</param>
        /// <param name="g">The green component.</param>
        /// <param name="b">The blue component.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Rgb24(byte r, byte g, byte b)
        {
            this.R = r;
            this.G = g;
            this.B = b;
        }

        /// <inheritdoc/>
        public PixelOperations<Rgb24> CreatePixelOperations() => new PixelOperations<Rgb24>();

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(Rgb24 other)
        {
            return this.R == other.R && this.G == other.G && this.B == other.B;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj?.GetType() == typeof(Rgb24) && this.Equals((Rgb24)obj);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = this.R;
                hashCode = (hashCode * 397) ^ this.G;
                hashCode = (hashCode * 397) ^ this.B;
                return hashCode;
            }
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromRgba32(Rgba32 source)
        {
            this = Unsafe.As<Rgba32, Rgb24>(ref source);
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
            return new Rgba32(this.R, this.G, this.B, 255).ToVector4();
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToRgb24(ref Rgb24 dest)
        {
            dest = this;
        }

        /// <inheritdoc/>
        public void ToRgba32(ref Rgba32 dest)
        {
            dest.Rgb = this;
            dest.A = 255;
        }

        /// <inheritdoc/>
        public void ToBgr24(ref Bgr24 dest)
        {
            dest.R = this.R;
            dest.G = this.G;
            dest.B = this.B;
        }

        /// <inheritdoc/>
        public void ToBgra32(ref Bgra32 dest)
        {
            dest.R = this.R;
            dest.G = this.G;
            dest.B = this.B;
            dest.A = 255;
        }
    }
}