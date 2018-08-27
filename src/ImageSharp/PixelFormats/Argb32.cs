// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.PixelFormats
{
    /// <summary>
    /// Packed pixel type containing four 8-bit unsigned normalized values ranging from 0 to 255.
    /// The color components are stored in alpha, red, green, and blue order (least significant to most significant byte).
    /// <para>
    /// Ranges from [0, 0, 0, 0] to [1, 1, 1, 1] in vector form.
    /// </para>
    /// </summary>
    /// <remarks>
    /// This struct is fully mutable. This is done (against the guidelines) for the sake of performance,
    /// as it avoids the need to create new values for modification operations.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential)]
    public struct Argb32 : IPixel<Argb32>, IPackedVector<uint>
    {
        /// <summary>
        /// Gets or sets the alpha component.
        /// </summary>
        public byte A;

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
        /// The maximum byte value.
        /// </summary>
        private static readonly Vector4 MaxBytes = new Vector4(255);

        /// <summary>
        /// The half vector value.
        /// </summary>
        private static readonly Vector4 Half = new Vector4(0.5F);

        /// <summary>
        /// Initializes a new instance of the <see cref="Argb32"/> struct.
        /// </summary>
        /// <param name="r">The red component.</param>
        /// <param name="g">The green component.</param>
        /// <param name="b">The blue component.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Argb32(byte r, byte g, byte b)
        {
            this.R = r;
            this.G = g;
            this.B = b;
            this.A = byte.MaxValue;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Argb32"/> struct.
        /// </summary>
        /// <param name="r">The red component.</param>
        /// <param name="g">The green component.</param>
        /// <param name="b">The blue component.</param>
        /// <param name="a">The alpha component.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Argb32(byte r, byte g, byte b, byte a)
        {
            this.R = r;
            this.G = g;
            this.B = b;
            this.A = a;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Argb32"/> struct.
        /// </summary>
        /// <param name="r">The red component.</param>
        /// <param name="g">The green component.</param>
        /// <param name="b">The blue component.</param>
        /// <param name="a">The alpha component.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Argb32(float r, float g, float b, float a = 1)
            : this()
        {
            this.Pack(r, g, b, a);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Argb32"/> struct.
        /// </summary>
        /// <param name="vector">
        /// The vector containing the components for the packed vector.
        /// </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Argb32(Vector3 vector)
            : this()
        {
            this.Pack(ref vector);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Argb32"/> struct.
        /// </summary>
        /// <param name="vector">
        /// The vector containing the components for the packed vector.
        /// </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Argb32(Vector4 vector)
            : this()
        {
            this.Pack(ref vector);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Argb32"/> struct.
        /// </summary>
        /// <param name="packed">
        /// The packed value.
        /// </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Argb32(uint packed)
            : this()
        {
            this.Argb = packed;
        }

        /// <summary>
        /// Gets or sets the packed representation of the Argb32 struct.
        /// </summary>
        public uint Argb
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Unsafe.As<Argb32, uint>(ref this);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => Unsafe.As<Argb32, uint>(ref this) = value;
        }

        /// <inheritdoc/>
        public uint PackedValue
        {
            get => this.Argb;
            set => this.Argb = value;
        }

        /// <summary>
        /// Compares two <see cref="Argb32"/> objects for equality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="Argb32"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="Argb32"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(Argb32 left, Argb32 right)
        {
            return left.Argb == right.Argb;
        }

        /// <summary>
        /// Compares two <see cref="Argb32"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="Argb32"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="Argb32"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(Argb32 left, Argb32 right)
        {
            return left.Argb != right.Argb;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromVector4(Vector4 vector)
        {
            this.Pack(ref vector);
        }

        /// <inheritdoc />
        public PixelOperations<Argb32> CreatePixelOperations() => new PixelOperations<Argb32>();

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
        public Vector4 ToVector4()
        {
            return new Vector4(this.R, this.G, this.B, this.A) / MaxBytes;
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
            this.PackedValue = source.PackedValue;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromBgra32(Bgra32 source)
        {
            this.R = source.R;
            this.G = source.G;
            this.B = source.B;
            this.A = source.A;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToRgb24(ref Rgb24 dest)
        {
            dest.R = this.R;
            dest.G = this.G;
            dest.B = this.B;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToRgba32(ref Rgba32 dest)
        {
            dest.R = this.R;
            dest.G = this.G;
            dest.B = this.B;
            dest.A = this.A;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToArgb32(ref Argb32 dest)
        {
            dest = this;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToBgr24(ref Bgr24 dest)
        {
            dest.R = this.R;
            dest.G = this.G;
            dest.B = this.B;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToBgra32(ref Bgra32 dest)
        {
            dest.R = this.R;
            dest.G = this.G;
            dest.B = this.B;
            dest.A = this.A;
        }

        /// <summary>
        /// Converts the pixel to <see cref="Rgba32"/> format.
        /// </summary>
        /// <returns>The RGBA value</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Rgba32 ToRgba32() => new Rgba32(this.R, this.G, this.B, this.A);

        /// <summary>
        /// Converts the pixel to <see cref="Bgra32"/> format.
        /// </summary>
        /// <returns>The RGBA value</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Bgra32 ToBgra32() => new Bgra32(this.R, this.G, this.B, this.A);

        /// <summary>
        /// Converts the pixel to <see cref="Argb32"/> format.
        /// </summary>
        /// <returns>The RGBA value</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Argb32 ToArgb32() => this;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromRgb48(Rgb48 source)
        {
            this.R = (byte)(((source.R * 255) + 32895) >> 16);
            this.G = (byte)(((source.G * 255) + 32895) >> 16);
            this.B = (byte)(((source.B * 255) + 32895) >> 16);
            this.A = byte.MaxValue;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToRgb48(ref Rgb48 dest) => dest.PackFromScaledVector4(this.ToScaledVector4());

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromRgba64(Rgba64 source)
        {
            this.R = (byte)(((source.R * 255) + 32895) >> 16);
            this.G = (byte)(((source.G * 255) + 32895) >> 16);
            this.B = (byte)(((source.B * 255) + 32895) >> 16);
            this.A = (byte)(((source.A * 255) + 32895) >> 16);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToRgba64(ref Rgba64 dest) => dest.PackFromScaledVector4(this.ToScaledVector4());

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is Argb32 argb32 && this.Equals(argb32);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(Argb32 other)
        {
            return this.Argb == other.Argb;
        }

        /// <summary>
        /// Gets a string representation of the packed vector.
        /// </summary>
        /// <returns>A string representation of the packed vector.</returns>
        public override string ToString()
        {
            return $"({this.R},{this.G},{this.B},{this.A})";
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode() => this.Argb.GetHashCode();

        /// <summary>
        /// Gets the <see cref="Vector4"/> representation without normalizing to [0, 1]
        /// </summary>
        /// <returns>A <see cref="Vector4"/> of values in [0, 255] </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Vector4 ToByteScaledVector4()
        {
            return new Vector4(this.R, this.G, this.B, this.A);
        }

        /// <summary>
        /// Packs the four floats into a color.
        /// </summary>
        /// <param name="x">The x-component</param>
        /// <param name="y">The y-component</param>
        /// <param name="z">The z-component</param>
        /// <param name="w">The w-component</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Pack(float x, float y, float z, float w)
        {
            var value = new Vector4(x, y, z, w);
            this.Pack(ref value);
        }

        /// <summary>
        /// Packs a <see cref="Vector3"/> into a uint.
        /// </summary>
        /// <param name="vector">The vector containing the values to pack.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Pack(ref Vector3 vector)
        {
            var value = new Vector4(vector, 1);
            this.Pack(ref value);
        }

        /// <summary>
        /// Packs a <see cref="Vector4"/> into a color.
        /// </summary>
        /// <param name="vector">The vector containing the values to pack.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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