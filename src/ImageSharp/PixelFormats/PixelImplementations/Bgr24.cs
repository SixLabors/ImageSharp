// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.PixelFormats
{
    /// <summary>
    /// Pixel type containing three 8-bit unsigned normalized values ranging from 0 to 255.
    /// The color components are stored in blue, green, red order (least significant to most significant byte).
    /// <para>
    /// Ranges from [0, 0, 0, 1] to [1, 1, 1, 1] in vector form.
    /// </para>
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    public partial struct Bgr24 : IPixel<Bgr24>
    {
        /// <summary>
        /// The blue component.
        /// </summary>
        [FieldOffset(0)]
        public byte B;

        /// <summary>
        /// The green component.
        /// </summary>
        [FieldOffset(1)]
        public byte G;

        /// <summary>
        /// The red component.
        /// </summary>
        [FieldOffset(2)]
        public byte R;

        /// <summary>
        /// Initializes a new instance of the <see cref="Bgr24"/> struct.
        /// </summary>
        /// <param name="r">The red component.</param>
        /// <param name="g">The green component.</param>
        /// <param name="b">The blue component.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public Bgr24(byte r, byte g, byte b)
        {
            this.R = r;
            this.G = g;
            this.B = b;
        }

        /// <summary>
        /// Converts an <see cref="Bgr24"/> to <see cref="Color"/>.
        /// </summary>
        /// <param name="source">The <see cref="Bgr24"/>.</param>
        /// <returns>The <see cref="Color"/>.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static implicit operator Color(Bgr24 source) => new Color(source);

        /// <summary>
        /// Converts a <see cref="Color"/> to <see cref="Bgr24"/>.
        /// </summary>
        /// <param name="color">The <see cref="Color"/>.</param>
        /// <returns>The <see cref="Bgr24"/>.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static implicit operator Bgr24(Color color) => color.ToBgr24();

        /// <summary>
        /// Compares two <see cref="Bgr24"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="Bgr24"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="Bgr24"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static bool operator ==(Bgr24 left, Bgr24 right) => left.Equals(right);

        /// <summary>
        /// Compares two <see cref="Bgr24"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="Bgr24"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="Bgr24"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static bool operator !=(Bgr24 left, Bgr24 right) => !left.Equals(right);

        /// <inheritdoc/>
        public readonly PixelOperations<Bgr24> CreatePixelOperations() => new PixelOperations();

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromScaledVector4(Vector4 vector) => this.FromVector4(vector);

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public readonly Vector4 ToScaledVector4() => this.ToVector4();

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromVector4(Vector4 vector)
        {
            Rgba32 rgba = default;
            rgba.FromVector4(vector);
            this.FromRgba32(rgba);
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public readonly Vector4 ToVector4() => new Rgba32(this.R, this.G, this.B, byte.MaxValue).ToVector4();

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromArgb32(Argb32 source)
        {
            this.R = source.R;
            this.G = source.G;
            this.B = source.B;
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromBgr24(Bgr24 source) => this = source;

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromBgra5551(Bgra5551 source) => this.FromScaledVector4(source.ToScaledVector4());

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromBgra32(Bgra32 source)
        {
            this.R = source.R;
            this.G = source.G;
            this.B = source.B;
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromL8(L8 source)
        {
            this.R = source.PackedValue;
            this.G = source.PackedValue;
            this.B = source.PackedValue;
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromL16(L16 source)
        {
            byte rgb = ColorNumerics.DownScaleFrom16BitTo8Bit(source.PackedValue);
            this.R = rgb;
            this.G = rgb;
            this.B = rgb;
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromLa16(La16 source)
        {
            this.R = source.L;
            this.G = source.L;
            this.B = source.L;
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromLa32(La32 source)
        {
            byte rgb = ColorNumerics.DownScaleFrom16BitTo8Bit(source.L);
            this.R = rgb;
            this.G = rgb;
            this.B = rgb;
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromRgb24(Rgb24 source)
        {
            this.R = source.R;
            this.G = source.G;
            this.B = source.B;
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromRgba32(Rgba32 source) => this = source.Bgr;

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public void ToRgba32(ref Rgba32 dest)
        {
            dest.R = this.R;
            dest.G = this.G;
            dest.B = this.B;
            dest.A = byte.MaxValue;
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromRgb48(Rgb48 source)
        {
            this.R = ColorNumerics.DownScaleFrom16BitTo8Bit(source.R);
            this.G = ColorNumerics.DownScaleFrom16BitTo8Bit(source.G);
            this.B = ColorNumerics.DownScaleFrom16BitTo8Bit(source.B);
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromRgba64(Rgba64 source)
        {
            this.R = ColorNumerics.DownScaleFrom16BitTo8Bit(source.R);
            this.G = ColorNumerics.DownScaleFrom16BitTo8Bit(source.G);
            this.B = ColorNumerics.DownScaleFrom16BitTo8Bit(source.B);
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public readonly bool Equals(Bgr24 other) => this.R.Equals(other.R) && this.G.Equals(other.G) && this.B.Equals(other.B);

        /// <inheritdoc/>
        public override readonly bool Equals(object obj) => obj is Bgr24 other && this.Equals(other);

        /// <inheritdoc />
        public override readonly string ToString() => $"Bgra({this.B}, {this.G}, {this.R})";

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public override readonly int GetHashCode() => HashCode.Combine(this.R, this.B, this.G);
    }
}
