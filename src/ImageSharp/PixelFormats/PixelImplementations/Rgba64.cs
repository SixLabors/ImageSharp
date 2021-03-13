// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.PixelFormats
{
    /// <summary>
    /// Packed pixel type containing four 16-bit unsigned normalized values ranging from 0 to 65535.
    /// <para>
    /// Ranges from [0, 0, 0, 0] to [1, 1, 1, 1] in vector form.
    /// </para>
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public partial struct Rgba64 : IPixel<Rgba64>, IPackedVector<ulong>
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
        [MethodImpl(InliningOptions.ShortMethod)]
        public Rgba64(ushort r, ushort g, ushort b, ushort a)
        {
            this.R = r;
            this.G = g;
            this.B = b;
            this.A = a;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Rgba64"/> struct.
        /// </summary>
        /// <param name="source">A structure of 4 bytes in RGBA byte order.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public Rgba64(Rgba32 source)
        {
            this.R = ColorNumerics.UpscaleFrom8BitTo16Bit(source.R);
            this.G = ColorNumerics.UpscaleFrom8BitTo16Bit(source.G);
            this.B = ColorNumerics.UpscaleFrom8BitTo16Bit(source.B);
            this.A = ColorNumerics.UpscaleFrom8BitTo16Bit(source.A);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Rgba64"/> struct.
        /// </summary>
        /// <param name="source">A structure of 4 bytes in BGRA byte order.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public Rgba64(Bgra32 source)
        {
            this.R = ColorNumerics.UpscaleFrom8BitTo16Bit(source.R);
            this.G = ColorNumerics.UpscaleFrom8BitTo16Bit(source.G);
            this.B = ColorNumerics.UpscaleFrom8BitTo16Bit(source.B);
            this.A = ColorNumerics.UpscaleFrom8BitTo16Bit(source.A);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Rgba64"/> struct.
        /// </summary>
        /// <param name="source">A structure of 4 bytes in ARGB byte order.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public Rgba64(Argb32 source)
        {
            this.R = ColorNumerics.UpscaleFrom8BitTo16Bit(source.R);
            this.G = ColorNumerics.UpscaleFrom8BitTo16Bit(source.G);
            this.B = ColorNumerics.UpscaleFrom8BitTo16Bit(source.B);
            this.A = ColorNumerics.UpscaleFrom8BitTo16Bit(source.A);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Rgba64"/> struct.
        /// </summary>
        /// <param name="source">A structure of 3 bytes in RGB byte order.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public Rgba64(Rgb24 source)
        {
            this.R = ColorNumerics.UpscaleFrom8BitTo16Bit(source.R);
            this.G = ColorNumerics.UpscaleFrom8BitTo16Bit(source.G);
            this.B = ColorNumerics.UpscaleFrom8BitTo16Bit(source.B);
            this.A = ushort.MaxValue;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Rgba64"/> struct.
        /// </summary>
        /// <param name="source">A structure of 3 bytes in BGR byte order.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public Rgba64(Bgr24 source)
        {
            this.R = ColorNumerics.UpscaleFrom8BitTo16Bit(source.R);
            this.G = ColorNumerics.UpscaleFrom8BitTo16Bit(source.G);
            this.B = ColorNumerics.UpscaleFrom8BitTo16Bit(source.B);
            this.A = ushort.MaxValue;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Rgba64"/> struct.
        /// </summary>
        /// <param name="vector">The <see cref="Vector4"/>.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public Rgba64(Vector4 vector)
        {
            vector = Numerics.Clamp(vector, Vector4.Zero, Vector4.One) * Max;
            this.R = (ushort)MathF.Round(vector.X);
            this.G = (ushort)MathF.Round(vector.Y);
            this.B = (ushort)MathF.Round(vector.Z);
            this.A = (ushort)MathF.Round(vector.W);
        }

        /// <summary>
        /// Gets or sets the RGB components of this struct as <see cref="Rgb48"/>.
        /// </summary>
        public Rgb48 Rgb
        {
            [MethodImpl(InliningOptions.ShortMethod)]
            readonly get => Unsafe.As<Rgba64, Rgb48>(ref Unsafe.AsRef(this));

            [MethodImpl(InliningOptions.ShortMethod)]
            set => Unsafe.As<Rgba64, Rgb48>(ref this) = value;
        }

        /// <inheritdoc/>
        public ulong PackedValue
        {
            [MethodImpl(InliningOptions.ShortMethod)]
            readonly get => Unsafe.As<Rgba64, ulong>(ref Unsafe.AsRef(this));

            [MethodImpl(InliningOptions.ShortMethod)]
            set => Unsafe.As<Rgba64, ulong>(ref this) = value;
        }

        /// <summary>
        /// Converts an <see cref="Rgba64"/> to <see cref="Color"/>.
        /// </summary>
        /// <param name="source">The <see cref="Rgba64"/>.</param>
        /// <returns>The <see cref="Color"/>.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static implicit operator Color(Rgba64 source) => new Color(source);

        /// <summary>
        /// Converts a <see cref="Color"/> to <see cref="Rgba64"/>.
        /// </summary>
        /// <param name="color">The <see cref="Color"/>.</param>
        /// <returns>The <see cref="Rgba64"/>.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static implicit operator Rgba64(Color color) => color.ToPixel<Rgba64>();

        /// <summary>
        /// Compares two <see cref="Rgba64"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="Rgba64"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="Rgba64"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static bool operator ==(Rgba64 left, Rgba64 right) => left.PackedValue == right.PackedValue;

        /// <summary>
        /// Compares two <see cref="Rgba64"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="Rgba64"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="Rgba64"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static bool operator !=(Rgba64 left, Rgba64 right) => left.PackedValue != right.PackedValue;

        /// <inheritdoc />
        public readonly PixelOperations<Rgba64> CreatePixelOperations() => new PixelOperations();

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
            this.A = (ushort)MathF.Round(vector.W);
        }

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public readonly Vector4 ToVector4() => new Vector4(this.R, this.G, this.B, this.A) / Max;

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromArgb32(Argb32 source)
        {
            this.R = ColorNumerics.UpscaleFrom8BitTo16Bit(source.R);
            this.G = ColorNumerics.UpscaleFrom8BitTo16Bit(source.G);
            this.B = ColorNumerics.UpscaleFrom8BitTo16Bit(source.B);
            this.A = ColorNumerics.UpscaleFrom8BitTo16Bit(source.A);
        }

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromBgr24(Bgr24 source)
        {
            this.R = ColorNumerics.UpscaleFrom8BitTo16Bit(source.R);
            this.G = ColorNumerics.UpscaleFrom8BitTo16Bit(source.G);
            this.B = ColorNumerics.UpscaleFrom8BitTo16Bit(source.B);
            this.A = ushort.MaxValue;
        }

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromBgra32(Bgra32 source)
        {
            this.R = ColorNumerics.UpscaleFrom8BitTo16Bit(source.R);
            this.G = ColorNumerics.UpscaleFrom8BitTo16Bit(source.G);
            this.B = ColorNumerics.UpscaleFrom8BitTo16Bit(source.B);
            this.A = ColorNumerics.UpscaleFrom8BitTo16Bit(source.A);
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromBgra5551(Bgra5551 source) => this.FromScaledVector4(source.ToScaledVector4());

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromL8(L8 source)
        {
            ushort rgb = ColorNumerics.UpscaleFrom8BitTo16Bit(source.PackedValue);
            this.R = rgb;
            this.G = rgb;
            this.B = rgb;
            this.A = ushort.MaxValue;
        }

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromL16(L16 source)
        {
            this.R = source.PackedValue;
            this.G = source.PackedValue;
            this.B = source.PackedValue;
            this.A = ushort.MaxValue;
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromLa16(La16 source)
        {
            ushort rgb = ColorNumerics.UpscaleFrom8BitTo16Bit(source.L);
            this.R = rgb;
            this.G = rgb;
            this.B = rgb;
            this.A = ColorNumerics.UpscaleFrom8BitTo16Bit(source.A);
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromLa32(La32 source)
        {
            this.R = source.L;
            this.G = source.L;
            this.B = source.L;
            this.A = source.A;
        }

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromRgb24(Rgb24 source)
        {
            this.R = ColorNumerics.UpscaleFrom8BitTo16Bit(source.R);
            this.G = ColorNumerics.UpscaleFrom8BitTo16Bit(source.G);
            this.B = ColorNumerics.UpscaleFrom8BitTo16Bit(source.B);
            this.A = ushort.MaxValue;
        }

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromRgba32(Rgba32 source)
        {
            this.R = ColorNumerics.UpscaleFrom8BitTo16Bit(source.R);
            this.G = ColorNumerics.UpscaleFrom8BitTo16Bit(source.G);
            this.B = ColorNumerics.UpscaleFrom8BitTo16Bit(source.B);
            this.A = ColorNumerics.UpscaleFrom8BitTo16Bit(source.A);
        }

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public void ToRgba32(ref Rgba32 dest)
        {
            dest.R = ColorNumerics.DownScaleFrom16BitTo8Bit(this.R);
            dest.G = ColorNumerics.DownScaleFrom16BitTo8Bit(this.G);
            dest.B = ColorNumerics.DownScaleFrom16BitTo8Bit(this.B);
            dest.A = ColorNumerics.DownScaleFrom16BitTo8Bit(this.A);
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromRgb48(Rgb48 source)
        {
            this.Rgb = source;
            this.A = ushort.MaxValue;
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromRgba64(Rgba64 source) => this = source;

        /// <summary>
        /// Convert to <see cref="Rgba32"/>.
        /// </summary>
        /// <returns>The <see cref="Rgba32"/>.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public readonly Rgba32 ToRgba32()
        {
            byte r = ColorNumerics.DownScaleFrom16BitTo8Bit(this.R);
            byte g = ColorNumerics.DownScaleFrom16BitTo8Bit(this.G);
            byte b = ColorNumerics.DownScaleFrom16BitTo8Bit(this.B);
            byte a = ColorNumerics.DownScaleFrom16BitTo8Bit(this.A);
            return new Rgba32(r, g, b, a);
        }

        /// <summary>
        /// Convert to <see cref="Bgra32"/>.
        /// </summary>
        /// <returns>The <see cref="Bgra32"/>.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public readonly Bgra32 ToBgra32()
        {
            byte r = ColorNumerics.DownScaleFrom16BitTo8Bit(this.R);
            byte g = ColorNumerics.DownScaleFrom16BitTo8Bit(this.G);
            byte b = ColorNumerics.DownScaleFrom16BitTo8Bit(this.B);
            byte a = ColorNumerics.DownScaleFrom16BitTo8Bit(this.A);
            return new Bgra32(r, g, b, a);
        }

        /// <summary>
        /// Convert to <see cref="Argb32"/>.
        /// </summary>
        /// <returns>The <see cref="Argb32"/>.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public readonly Argb32 ToArgb32()
        {
            byte r = ColorNumerics.DownScaleFrom16BitTo8Bit(this.R);
            byte g = ColorNumerics.DownScaleFrom16BitTo8Bit(this.G);
            byte b = ColorNumerics.DownScaleFrom16BitTo8Bit(this.B);
            byte a = ColorNumerics.DownScaleFrom16BitTo8Bit(this.A);
            return new Argb32(r, g, b, a);
        }

        /// <summary>
        /// Convert to <see cref="Rgb24"/>.
        /// </summary>
        /// <returns>The <see cref="Rgb24"/>.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public readonly Rgb24 ToRgb24()
        {
            byte r = ColorNumerics.DownScaleFrom16BitTo8Bit(this.R);
            byte g = ColorNumerics.DownScaleFrom16BitTo8Bit(this.G);
            byte b = ColorNumerics.DownScaleFrom16BitTo8Bit(this.B);
            return new Rgb24(r, g, b);
        }

        /// <summary>
        /// Convert to <see cref="Bgr24"/>.
        /// </summary>
        /// <returns>The <see cref="Bgr24"/>.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public readonly Bgr24 ToBgr24()
        {
            byte r = ColorNumerics.DownScaleFrom16BitTo8Bit(this.R);
            byte g = ColorNumerics.DownScaleFrom16BitTo8Bit(this.G);
            byte b = ColorNumerics.DownScaleFrom16BitTo8Bit(this.B);
            return new Bgr24(r, g, b);
        }

        /// <inheritdoc />
        public override readonly bool Equals(object obj) => obj is Rgba64 rgba64 && this.Equals(rgba64);

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public readonly bool Equals(Rgba64 other) => this.PackedValue.Equals(other.PackedValue);

        /// <inheritdoc />
        public override readonly string ToString() => $"Rgba64({this.R}, {this.G}, {this.B}, {this.A})";

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public override readonly int GetHashCode() => this.PackedValue.GetHashCode();
    }
}
