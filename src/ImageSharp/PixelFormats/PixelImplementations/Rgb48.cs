// Copyright (c) Six Labors.
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
    public partial struct Rgb48 : IPixel<Rgb48>
    {
        private const float Max = ushort.MaxValue;

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
        /// Compares two <see cref="Rgb48"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="Rgb48"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="Rgb48"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static bool operator ==(Rgb48 left, Rgb48 right) => left.Equals(right);

        /// <summary>
        /// Compares two <see cref="Rgb48"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="Rgb48"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="Rgb48"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static bool operator !=(Rgb48 left, Rgb48 right) => !left.Equals(right);

        /// <inheritdoc />
        public readonly PixelOperations<Rgb48> CreatePixelOperations() => new PixelOperations();

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromScaledVector4(Vector4 vector) => this.FromVector4(vector);

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public readonly Vector4 ToScaledVector4() => this.ToVector4();

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromVector4(Vector4 vector)
        {
            vector = Numerics.Clamp(vector, Vector4.Zero, Vector4.One) * Max;
            this.R = (ushort)MathF.Round(vector.X);
            this.G = (ushort)MathF.Round(vector.Y);
            this.B = (ushort)MathF.Round(vector.Z);
        }

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public readonly Vector4 ToVector4() => new Vector4(this.R / Max, this.G / Max, this.B / Max, 1F);

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromArgb32(Argb32 source)
        {
            this.R = ColorNumerics.UpscaleFrom8BitTo16Bit(source.R);
            this.G = ColorNumerics.UpscaleFrom8BitTo16Bit(source.G);
            this.B = ColorNumerics.UpscaleFrom8BitTo16Bit(source.B);
        }

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromBgr24(Bgr24 source)
        {
            this.R = ColorNumerics.UpscaleFrom8BitTo16Bit(source.R);
            this.G = ColorNumerics.UpscaleFrom8BitTo16Bit(source.G);
            this.B = ColorNumerics.UpscaleFrom8BitTo16Bit(source.B);
        }

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromBgra32(Bgra32 source)
        {
            this.R = ColorNumerics.UpscaleFrom8BitTo16Bit(source.R);
            this.G = ColorNumerics.UpscaleFrom8BitTo16Bit(source.G);
            this.B = ColorNumerics.UpscaleFrom8BitTo16Bit(source.B);
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromRgba64(Rgba64 source) => this = source.Rgb;

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromBgra5551(Bgra5551 source) => this.FromScaledVector4(source.ToScaledVector4());

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromL8(L8 source)
        {
            ushort rgb = ColorNumerics.UpscaleFrom8BitTo16Bit(source.PackedValue);
            this.R = rgb;
            this.G = rgb;
            this.B = rgb;
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromL16(L16 source)
        {
            this.R = source.PackedValue;
            this.G = source.PackedValue;
            this.B = source.PackedValue;
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromLa16(La16 source)
        {
            ushort rgb = ColorNumerics.UpscaleFrom8BitTo16Bit(source.L);
            this.R = rgb;
            this.G = rgb;
            this.B = rgb;
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromLa32(La32 source)
        {
            this.R = source.L;
            this.G = source.L;
            this.B = source.L;
        }

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromRgb24(Rgb24 source)
        {
            this.R = ColorNumerics.UpscaleFrom8BitTo16Bit(source.R);
            this.G = ColorNumerics.UpscaleFrom8BitTo16Bit(source.G);
            this.B = ColorNumerics.UpscaleFrom8BitTo16Bit(source.B);
        }

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromRgba32(Rgba32 source)
        {
            this.R = ColorNumerics.UpscaleFrom8BitTo16Bit(source.R);
            this.G = ColorNumerics.UpscaleFrom8BitTo16Bit(source.G);
            this.B = ColorNumerics.UpscaleFrom8BitTo16Bit(source.B);
        }

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public void ToRgba32(ref Rgba32 dest)
        {
            dest.R = ColorNumerics.DownScaleFrom16BitTo8Bit(this.R);
            dest.G = ColorNumerics.DownScaleFrom16BitTo8Bit(this.G);
            dest.B = ColorNumerics.DownScaleFrom16BitTo8Bit(this.B);
            dest.A = byte.MaxValue;
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromRgb48(Rgb48 source) => this = source;

        /// <inheritdoc />
        public override readonly bool Equals(object obj) => obj is Rgb48 rgb48 && this.Equals(rgb48);

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public readonly bool Equals(Rgb48 other) => this.R.Equals(other.R) && this.G.Equals(other.G) && this.B.Equals(other.B);

        /// <inheritdoc />
        public override readonly string ToString() => $"Rgb48({this.R}, {this.G}, {this.B})";

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public override readonly int GetHashCode() => HashCode.Combine(this.R, this.G, this.B);
    }
}
