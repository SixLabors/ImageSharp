﻿// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.PixelFormats
{
    /// <summary>
    /// Packed pixel type containing four 8-bit unsigned normalized values ranging from 0 to 255.
    /// The color components are stored in red, green, blue, and alpha order (least significant to most significant byte).
    /// <para>
    /// Ranges from [0, 0, 0, 0] to [1, 1, 1, 1] in vector form.
    /// </para>
    /// </summary>
    /// <remarks>
    /// This struct is fully mutable. This is done (against the guidelines) for the sake of performance,
    /// as it avoids the need to create new values for modification operations.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential)]
    public partial struct Rgba32 : IPixel<Rgba32>, IPackedVector<uint>
    {
        /// <summary>
        /// Gets or sets the red component.
        /// </summary>
        public byte R;

        /// <summary>
        /// Gets or sets the green component.
        /// </summary>
        public byte G;

        /// <summary>
        /// Gets or sets the blue component.
        /// </summary>
        public byte B;

        /// <summary>
        /// Gets or sets the alpha component.
        /// </summary>
        public byte A;

        /// <summary>
        /// The shift count for the red component
        /// </summary>
        private const int RedShift = 0;

        /// <summary>
        /// The shift count for the green component
        /// </summary>
        private const int GreenShift = 8;

        /// <summary>
        /// The shift count for the blue component
        /// </summary>
        private const int BlueShift = 16;

        /// <summary>
        /// The shift count for the alpha component
        /// </summary>
        private const int AlphaShift = 24;

        /// <summary>
        /// The maximum byte value.
        /// </summary>
        private static readonly Vector4 MaxBytes = new Vector4(255);

        /// <summary>
        /// The half vector value.
        /// </summary>
        private static readonly Vector4 Half = new Vector4(0.5F);

        /// <summary>
        /// Initializes a new instance of the <see cref="Rgba32"/> struct.
        /// </summary>
        /// <param name="r">The red component.</param>
        /// <param name="g">The green component.</param>
        /// <param name="b">The blue component.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public Rgba32(byte r, byte g, byte b)
        {
            this.R = r;
            this.G = g;
            this.B = b;
            this.A = byte.MaxValue;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Rgba32"/> struct.
        /// </summary>
        /// <param name="r">The red component.</param>
        /// <param name="g">The green component.</param>
        /// <param name="b">The blue component.</param>
        /// <param name="a">The alpha component.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public Rgba32(byte r, byte g, byte b, byte a)
        {
            this.R = r;
            this.G = g;
            this.B = b;
            this.A = a;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Rgba32"/> struct.
        /// </summary>
        /// <param name="r">The red component.</param>
        /// <param name="g">The green component.</param>
        /// <param name="b">The blue component.</param>
        /// <param name="a">The alpha component.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public Rgba32(float r, float g, float b, float a = 1)
            : this() => this.Pack(r, g, b, a);

        /// <summary>
        /// Initializes a new instance of the <see cref="Rgba32"/> struct.
        /// </summary>
        /// <param name="vector">
        /// The vector containing the components for the packed vector.
        /// </param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public Rgba32(Vector3 vector)
            : this() => this.Pack(ref vector);

        /// <summary>
        /// Initializes a new instance of the <see cref="Rgba32"/> struct.
        /// </summary>
        /// <param name="vector">
        /// The vector containing the components for the packed vector.
        /// </param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public Rgba32(Vector4 vector)
            : this() => this = PackNew(ref vector);

        /// <summary>
        /// Initializes a new instance of the <see cref="Rgba32"/> struct.
        /// </summary>
        /// <param name="packed">
        /// The packed value.
        /// </param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public Rgba32(uint packed)
            : this() => this.Rgba = packed;

        /// <summary>
        /// Gets or sets the packed representation of the Rgba32 struct.
        /// </summary>
        public uint Rgba
        {
            [MethodImpl(InliningOptions.ShortMethod)]
            get => Unsafe.As<Rgba32, uint>(ref this);

            [MethodImpl(InliningOptions.ShortMethod)]
            set => Unsafe.As<Rgba32, uint>(ref this) = value;
        }

        /// <summary>
        /// Gets or sets the RGB components of this struct as <see cref="Rgb24"/>
        /// </summary>
        public Rgb24 Rgb
        {
            [MethodImpl(InliningOptions.ShortMethod)]
            get => Unsafe.As<Rgba32, Rgb24>(ref this);

            [MethodImpl(InliningOptions.ShortMethod)]
            set => Unsafe.As<Rgba32, Rgb24>(ref this) = value;
        }

        /// <summary>
        /// Gets or sets the RGB components of this struct as <see cref="Bgr24"/> reverting the component order.
        /// </summary>
        public Bgr24 Bgr
        {
            [MethodImpl(InliningOptions.ShortMethod)]
            get => new Bgr24(this.R, this.G, this.B);

            [MethodImpl(InliningOptions.ShortMethod)]
            set
            {
                this.R = value.R;
                this.G = value.G;
                this.B = value.B;
            }
        }

        /// <inheritdoc/>
        public uint PackedValue
        {
            [MethodImpl(InliningOptions.ShortMethod)]
            get => this.Rgba;

            [MethodImpl(InliningOptions.ShortMethod)]
            set => this.Rgba = value;
        }

        /// <summary>
        /// Compares two <see cref="Rgba32"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="Rgba32"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="Rgba32"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static bool operator ==(Rgba32 left, Rgba32 right) => left.Equals(right);

        /// <summary>
        /// Compares two <see cref="Rgba32"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="Rgba32"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="Rgba32"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static bool operator !=(Rgba32 left, Rgba32 right) => !left.Equals(right);

        /// <summary>
        /// Creates a new instance of the <see cref="Rgba32"/> struct.
        /// </summary>
        /// <param name="hex">
        /// The hexadecimal representation of the combined color components arranged
        /// in rgb, rgba, rrggbb, or rrggbbaa format to match web syntax.
        /// </param>
        /// <returns>
        /// The <see cref="Rgba32"/>.
        /// </returns>
        public static Rgba32 FromHex(string hex) => ColorBuilder<Rgba32>.FromHex(hex);

        /// <inheritdoc />
        public PixelOperations<Rgba32> CreatePixelOperations() => new PixelOperations();

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void PackFromScaledVector4(Vector4 vector) => this.PackFromVector4(vector);

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public Vector4 ToScaledVector4() => this.ToVector4();

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void PackFromVector4(Vector4 vector) => this.Pack(ref vector);

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public Vector4 ToVector4() => new Vector4(this.R, this.G, this.B, this.A) / MaxBytes;

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void PackFromArgb32(Argb32 source)
        {
            this.R = source.R;
            this.G = source.G;
            this.B = source.B;
            this.A = source.A;
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void PackFromBgra32(Bgra32 source)
        {
            this.R = source.R;
            this.G = source.G;
            this.B = source.B;
            this.A = source.A;
        }

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public void PackFromGray8(Gray8 source)
        {
            this.R = source.PackedValue;
            this.G = source.PackedValue;
            this.B = source.PackedValue;
            this.A = byte.MaxValue;
        }

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public void PackFromGray16(Gray16 source)
        {
            byte rgb = ImageMaths.DownScaleFrom16BitTo8Bit(source.PackedValue);
            this.R = rgb;
            this.G = rgb;
            this.B = rgb;
            this.A = byte.MaxValue;
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void PackFromRgba32(Rgba32 source) => this = source;

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public Rgba32 ToRgba32() => this;

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void PackFromRgb48(Rgb48 source)
        {
            this.R = ImageMaths.DownScaleFrom16BitTo8Bit(source.R);
            this.G = ImageMaths.DownScaleFrom16BitTo8Bit(source.G);
            this.B = ImageMaths.DownScaleFrom16BitTo8Bit(source.B);
            this.A = byte.MaxValue;
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void PackFromRgba64(Rgba64 source)
        {
            this.R = ImageMaths.DownScaleFrom16BitTo8Bit(source.R);
            this.G = ImageMaths.DownScaleFrom16BitTo8Bit(source.G);
            this.B = ImageMaths.DownScaleFrom16BitTo8Bit(source.B);
            this.A = ImageMaths.DownScaleFrom16BitTo8Bit(source.A);
        }

        /// <summary>
        /// Converts the value of this instance to a hexadecimal string.
        /// </summary>
        /// <returns>A hexadecimal string representation of the value.</returns>
        public string ToHex()
        {
            uint hexOrder = (uint)(this.A << 0 | this.B << 8 | this.G << 16 | this.R << 24);
            return hexOrder.ToString("X8");
        }

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is Rgba32 rgba32 && this.Equals(rgba32);

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public bool Equals(Rgba32 other) => this.Rgba.Equals(other.Rgba);

        /// <inheritdoc/>
        public override string ToString() => $"Rgba32({this.R}, {this.G}, {this.B}, {this.A})";

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public override int GetHashCode() => this.Rgba.GetHashCode();

        /// <summary>
        /// Packs a <see cref="Vector4"/> into a color returning a new instance as a result.
        /// </summary>
        /// <param name="vector">The vector containing the values to pack.</param>
        /// <returns>The <see cref="Rgba32"/></returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        private static Rgba32 PackNew(ref Vector4 vector)
        {
            vector *= MaxBytes;
            vector += Half;
            vector = Vector4.Clamp(vector, Vector4.Zero, MaxBytes);

            return new Rgba32((byte)vector.X, (byte)vector.Y, (byte)vector.Z, (byte)vector.W);
        }

        /// <summary>
        /// Packs the four floats into a color.
        /// </summary>
        /// <param name="x">The x-component</param>
        /// <param name="y">The y-component</param>
        /// <param name="z">The z-component</param>
        /// <param name="w">The w-component</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        private void Pack(float x, float y, float z, float w)
        {
            var value = new Vector4(x, y, z, w);
            this.Pack(ref value);
        }

        /// <summary>
        /// Packs a <see cref="Vector3"/> into a uint.
        /// </summary>
        /// <param name="vector">The vector containing the values to pack.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        private void Pack(ref Vector3 vector)
        {
            var value = new Vector4(vector, 1F);
            this.Pack(ref value);
        }

        /// <summary>
        /// Packs a <see cref="Vector4"/> into a color.
        /// </summary>
        /// <param name="vector">The vector containing the values to pack.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        private void Pack(ref Vector4 vector)
        {
            vector *= MaxBytes;
            vector += Half;
            vector = Vector4.Clamp(vector, Vector4.Zero, MaxBytes);

            this.R = (byte)vector.X;
            this.G = (byte)vector.Y;
            this.B = (byte)vector.Z;
            this.A = (byte)vector.W;
        }
    }
}