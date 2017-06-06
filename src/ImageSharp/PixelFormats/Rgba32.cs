// <copyright file="Rgba32.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Numerics;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    using ImageSharp.PixelFormats;

    /// <summary>
    /// Packed pixel type containing four 8-bit unsigned normalized values ranging from 0 to 255.
    /// The color components are stored in red, green, blue, and alpha order.
    /// <para>
    /// Ranges from &lt;0, 0, 0, 0&gt; to &lt;1, 1, 1, 1&gt; in vector form.
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Rgba32(byte r, byte g, byte b)
        {
            this.R = r;
            this.G = g;
            this.B = b;
            this.A = 255;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Rgba32"/> struct.
        /// </summary>
        /// <param name="r">The red component.</param>
        /// <param name="g">The green component.</param>
        /// <param name="b">The blue component.</param>
        /// <param name="a">The alpha component.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Rgba32(float r, float g, float b, float a = 1)
            : this()
        {
            this.Pack(r, g, b, a);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Rgba32"/> struct.
        /// </summary>
        /// <param name="vector">
        /// The vector containing the components for the packed vector.
        /// </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Rgba32(Vector3 vector)
            : this()
        {
            this.Pack(ref vector);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Rgba32"/> struct.
        /// </summary>
        /// <param name="vector">
        /// The vector containing the components for the packed vector.
        /// </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Rgba32(Vector4 vector)
            : this()
        {
            this = PackNew(ref vector);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Rgba32"/> struct.
        /// </summary>
        /// <param name="packed">
        /// The packed value.
        /// </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Rgba32(uint packed)
            : this()
        {
            this.Rgba = packed;
        }

        /// <summary>
        /// Gets or sets the packed representation of the Rgba32 struct.
        /// </summary>
        public uint Rgba
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return Unsafe.As<Rgba32, uint>(ref this);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                Unsafe.As<Rgba32, uint>(ref this) = value;
            }
        }

        /// <summary>
        /// Gets or sets the RGB components of this struct as <see cref="Rgb24"/>
        /// </summary>
        public Rgb24 Rgb
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return Unsafe.As<Rgba32, Rgb24>(ref this);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                Unsafe.As<Rgba32, Rgb24>(ref this) = value;
            }
        }

        /// <summary>
        /// Gets or sets the RGB components of this struct as <see cref="Bgr24"/> reverting the component order.
        /// </summary>
        public Bgr24 Bgr
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return new Bgr24(this.R, this.G, this.B);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
            get => this.Rgba;
            set => this.Rgba = value;
        }

        /// <summary>
        /// Compares two <see cref="Rgba32"/> objects for equality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="Rgba32"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="Rgba32"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(Rgba32 left, Rgba32 right)
        {
            return left.Rgba == right.Rgba;
        }

        /// <summary>
        /// Compares two <see cref="Rgba32"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="Rgba32"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="Rgba32"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(Rgba32 left, Rgba32 right)
        {
            return left.Rgba != right.Rgba;
        }

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
        public static Rgba32 FromHex(string hex)
        {
            return ColorBuilder<Rgba32>.FromHex(hex);
        }

        /// <inheritdoc />
        public PixelOperations<Rgba32> CreatePixelOperations() => new PixelOperations();

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromRgba32(Rgba32 source)
        {
            this = source;
        }

        /// <summary>
        /// Converts the value of this instance to a hexadecimal string.
        /// </summary>
        /// <returns>A hexadecimal string representation of the value.</returns>
        public string ToHex()
        {
            uint hexOrder = Pack(this.A, this.B, this.G, this.R);
            return hexOrder.ToString("X8");
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToRgb24(ref Rgb24 dest)
        {
            dest = Unsafe.As<Rgba32, Rgb24>(ref this);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToRgba32(ref Rgba32 dest)
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

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromVector4(Vector4 vector)
        {
            this.Pack(ref vector);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector4 ToVector4()
        {
            return new Vector4(this.R, this.G, this.B, this.A) / MaxBytes;
        }

        /// <summary>
        /// Gets the value of this struct as <see cref="Bgra32"/>.
        /// Useful for changing the component order.
        /// </summary>
        /// <returns>A <see cref="Bgra32"/> value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Bgra32 ToBgra32()
        {
            return new Bgra32(this.R, this.G, this.B, this.A);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return (obj is Rgba32) && this.Equals((Rgba32)obj);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(Rgba32 other)
        {
            return this.Rgba == other.Rgba;
        }

        /// <summary>
        /// Gets a string representation of the packed vector.
        /// </summary>
        /// <returns>A string representation of the packed vector.</returns>
        public override string ToString()
        {
            return this.ToVector4().ToString();
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = this.R;
                hashCode = (hashCode * 397) ^ this.G;
                hashCode = (hashCode * 397) ^ this.B;
                hashCode = (hashCode * 397) ^ this.A;
                return hashCode;
            }
        }

        /// <summary>
        /// Gets the <see cref="Vector4"/> representation without normalizing to [0, 1]
        /// </summary>
        /// <returns>A <see cref="Vector4"/> of values in [0, 255] </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Vector4 ToUnscaledVector4()
        {
            return new Vector4(this.R, this.G, this.B, this.A);
        }

        /// <summary>
        /// Packs the four floats into a <see cref="uint"/>.
        /// </summary>
        /// <param name="x">The x-component</param>
        /// <param name="y">The y-component</param>
        /// <param name="z">The z-component</param>
        /// <param name="w">The w-component</param>
        /// <returns>The <see cref="uint"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Pack(byte x, byte y, byte z, byte w)
        {
            return (uint)(x << RedShift | y << GreenShift | z << BlueShift | w << AlphaShift);
        }

        /// <summary>
        /// Packs a <see cref="Vector4"/> into a color returning a new instance as a result.
        /// </summary>
        /// <param name="vector">The vector containing the values to pack.</param>
        /// <returns>The <see cref="Rgba32"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Pack(float x, float y, float z, float w)
        {
            Vector4 value = new Vector4(x, y, z, w);
            this.Pack(ref value);
        }

        /// <summary>
        /// Packs a <see cref="Vector3"/> into a uint.
        /// </summary>
        /// <param name="vector">The vector containing the values to pack.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Pack(ref Vector3 vector)
        {
            Vector4 value = new Vector4(vector, 1);
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