// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.PixelFormats
{
    /// <summary>
    /// Packed pixel type containing a single 16 bit normalized gray values.
    /// <para>
    /// Ranges from [0, 0, 0, 1] to [1, 1, 1, 1] in vector form.
    /// </para>
    /// </summary>
    public struct Gray16 : IPixel<Gray16>, IPackedVector<ushort>
    {
        /// <summary>
        /// RX as in ITU-R recommendation 709 to match libpng
        /// </summary>
        private const float Rx = .2126F;

        /// <summary>
        /// GX as in ITU-R recommendation 709 to match libpng
        /// </summary>
        private const float Gx = .7152F;

        /// <summary>
        /// BX as in ITU-R recommendation 709 to match libpng
        /// </summary>
        private const float Bx = .0722F;

        /// <summary>
        /// Initializes a new instance of the <see cref="Gray16"/> struct.
        /// </summary>
        /// <param name="gray">The gray component</param>
        public Gray16(byte gray)
        {
            this.PackedValue = gray;
        }

        /// <inheritdoc />
        public ushort PackedValue { get; set; }

        /// <summary>
        /// Compares two <see cref="Gray16"/> objects for equality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="Gray16"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="Gray16"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(Gray16 left, Gray16 right)
        {
            return left.PackedValue == right.PackedValue;
        }

        /// <summary>
        /// Compares two <see cref="Gray16"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="Gray16"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="Gray16"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(Gray16 left, Gray16 right)
        {
            return left.PackedValue != right.PackedValue;
        }

        /// <inheritdoc />
        public PixelOperations<Gray16> CreatePixelOperations() => new PixelOperations<Gray16>();

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
            var scaledGray = this.PackedValue / 65535f; // ushort.Max as float
            return new Vector4(scaledGray, scaledGray, scaledGray, 1.0f);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromVector4(Vector4 vector)
        {
            this.PackedValue = Pack(vector.X, vector.Y, vector.Z);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector4 ToVector4()
        {
            return new Vector4(this.PackedValue, this.PackedValue, this.PackedValue, 1.0f);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromRgba32(Rgba32 source)
        {
            this.PackedValue = Pack(source.R, source.G, source.B);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromArgb32(Argb32 source)
        {
            this.PackedValue = Pack(source.R, source.G, source.B);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromBgra32(Bgra32 source)
        {
            this.PackedValue = Pack(source.R, source.G, source.B);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToRgb24(ref Rgb24 dest)
        {
            var scaledValue = this.PackedAsByte();

            dest.R = scaledValue;
            dest.G = scaledValue;
            dest.B = scaledValue;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToRgba32(ref Rgba32 dest)
        {
            var scaledValue = this.PackedAsByte();

            dest.R = scaledValue;
            dest.G = scaledValue;
            dest.B = scaledValue;
            dest.A = 255;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToArgb32(ref Argb32 dest)
        {
            var scaledValue = this.PackedAsByte();

            dest.R = scaledValue;
            dest.G = scaledValue;
            dest.B = scaledValue;
            dest.A = 255;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToBgr24(ref Bgr24 dest)
        {
            var scaledValue = this.PackedAsByte();

            dest.R = scaledValue;
            dest.G = scaledValue;
            dest.B = scaledValue;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToBgra32(ref Bgra32 dest)
        {
            var scaledValue = this.PackedAsByte();

            dest.R = scaledValue;
            dest.G = scaledValue;
            dest.B = scaledValue;
            dest.A = 255;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromRgb48(Rgb48 source) =>
            this.PackedValue = Pack(source.R, source.G, source.B);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToRgb48(ref Rgb48 dest)
        {
            dest.R = this.PackedValue;
            dest.G = this.PackedValue;
            dest.B = this.PackedValue;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromRgba64(Rgba64 source) =>
            this.PackFromScaledVector4(source.ToScaledVector4());

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToRgba64(ref Rgba64 dest) => dest.PackFromScaledVector4(this.ToScaledVector4());

        /// <summary>
        /// Compares an object with the packed vector.
        /// </summary>
        /// <param name="obj">The object to compare.</param>
        /// <returns>True if the object is equal to the packed vector.</returns>
        public override bool Equals(object obj)
        {
            return obj is Gray16 other && this.Equals(other);
        }

        /// <summary>
        /// Compares another <see cref="Gray16" /> packed vector with the packed vector.
        /// </summary>
        /// <param name="other">The Gray8 packed vector to compare.</param>
        /// <returns>True if the packed vectors are equal.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(Gray16 other)
        {
            return this.PackedValue == other.PackedValue;
        }

        /// <summary>
        /// Gets a string representation of the packed vector.
        /// </summary>
        /// <returns>A string representation of the packed vector.</returns>
        public override string ToString()
        {
            return (this.PackedValue / 65535f).ToString();
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode() => this.PackedValue.GetHashCode();

        /// <summary>
        /// Packs a <see cref="float"/> into a byte.
        /// </summary>
        /// <param name="r">Red value of the color to pack.</param>
        /// <param name="g">Green value of the color to pack.</param>
        /// <param name="b">Blue value of the color to pack.</param>
        /// <returns>The <see cref="ushort"/> containing the packed value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ushort Pack(float r, float g, float b)
        {
            float sum = r + g + b;
            float val = (r * Rx) + (g * Gx) + (b * Bx);
            return (ushort)Math.Round(val * 65535f / sum);  // TODO: if this is correct, Rx, Gx, Bx consts could be scaled by 65535f directly!
        }

        /// <summary>
        /// Packs the <see cref="ushort" /> into a byte.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte PackedAsByte()
        {
            return (byte)(this.PackedValue >> 8);
        }
    }
}