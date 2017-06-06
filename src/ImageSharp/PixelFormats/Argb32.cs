// <copyright file="Argb32.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.PixelFormats
{
    using System;
    using System.Numerics;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Packed pixel type containing four 8-bit unsigned normalized values ranging from 0 to 255.
    /// The color components are stored in alpha, red, green, and blue order.
    /// <para>
    /// Ranges from &lt;0, 0, 0, 0&gt; to &lt;1, 1, 1, 1&gt; in vector form.
    /// </para>
    /// </summary>
    /// <remarks>
    /// This struct is fully mutable. This is done (against the guidelines) for the sake of performance,
    /// as it avoids the need to create new values for modification operations.
    /// </remarks>
    public struct Argb32 : IPixel<Argb32>, IPackedVector<uint>
    {
        /// <summary>
        /// The shift count for the blue component
        /// </summary>
        private const int BlueShift = 0;

        /// <summary>
        /// The shift count for the green component
        /// </summary>
        private const int GreenShift = 8;

        /// <summary>
        /// The shift count for the red component
        /// </summary>
        private const int RedShift = 16;

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
        /// Initializes a new instance of the <see cref="Argb32"/> struct.
        /// </summary>
        /// <param name="r">The red component.</param>
        /// <param name="g">The green component.</param>
        /// <param name="b">The blue component.</param>
        /// <param name="a">The alpha component.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Argb32(byte r, byte g, byte b, byte a)
        {
            this.PackedValue = Pack(r, g, b, a);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Argb32"/> struct.
        /// </summary>
        /// <param name="r">The red component.</param>
        /// <param name="g">The green component.</param>
        /// <param name="b">The blue component.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Argb32(byte r, byte g, byte b)
        {
            this.PackedValue = Pack(r, g, b, 255);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Argb32"/> struct.
        /// </summary>
        /// <param name="r">The red component.</param>
        /// <param name="g">The green component.</param>
        /// <param name="b">The blue component.</param>
        /// <param name="a">The alpha component.</param>
        public Argb32(float r, float g, float b, float a = 1)
        {
            this.PackedValue = Pack(r, g, b, a);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Argb32"/> struct.
        /// </summary>
        /// <param name="vector">
        /// The vector containing the components for the packed vector.
        /// </param>
        public Argb32(Vector3 vector)
        {
            this.PackedValue = Pack(ref vector);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Argb32"/> struct.
        /// </summary>
        /// <param name="vector">
        /// The vector containing the components for the packed vector.
        /// </param>
        public Argb32(Vector4 vector)
        {
            this.PackedValue = Pack(ref vector);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Argb32"/> struct.
        /// </summary>
        /// <param name="packed">
        /// The packed value.
        /// </param>
        public Argb32(uint packed = 0)
        {
            this.PackedValue = packed;
        }

        /// <inheritdoc/>
        public uint PackedValue { get; set; }

        /// <summary>
        /// Gets or sets the red component.
        /// </summary>
        public byte R
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return (byte)(this.PackedValue >> RedShift);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                this.PackedValue = this.PackedValue & 0xFF00FFFF | (uint)value << RedShift;
            }
        }

        /// <summary>
        /// Gets or sets the green component.
        /// </summary>
        public byte G
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return (byte)(this.PackedValue >> GreenShift);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                this.PackedValue = this.PackedValue & 0xFFFF00FF | (uint)value << GreenShift;
            }
        }

        /// <summary>
        /// Gets or sets the blue component.
        /// </summary>
        public byte B
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return (byte)(this.PackedValue >> BlueShift);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                this.PackedValue = this.PackedValue & 0xFFFFFF00 | (uint)value << BlueShift;
            }
        }

        /// <summary>
        /// Gets or sets the alpha component.
        /// </summary>
        public byte A
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return (byte)(this.PackedValue >> AlphaShift);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                this.PackedValue = this.PackedValue & 0x00FFFFFF | (uint)value << AlphaShift;
            }
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
            return left.PackedValue == right.PackedValue;
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
            return left.PackedValue != right.PackedValue;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromVector4(Vector4 vector)
        {
            this.PackedValue = Pack(ref vector);
        }

        /// <inheritdoc />
        public PixelOperations<Argb32> CreatePixelOperations() => new PixelOperations<Argb32>();

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
            this.PackedValue = Pack(source.R, source.G, source.B, source.A);
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
        public override bool Equals(object obj)
        {
            return obj is Argb32 && this.Equals((Argb32)obj);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(Argb32 other)
        {
            return this.PackedValue == other.PackedValue;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            // ReSharper disable once NonReadonlyMemberInGetHashCode
            return this.PackedValue.GetHashCode();
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
        private static uint Pack(float x, float y, float z, float w)
        {
            Vector4 value = new Vector4(x, y, z, w);
            return Pack(ref value);
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
        /// Packs a <see cref="Vector3"/> into a uint.
        /// </summary>
        /// <param name="vector">The vector containing the values to pack.</param>
        /// <returns>The <see cref="uint"/> containing the packed values.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Pack(ref Vector3 vector)
        {
            Vector4 value = new Vector4(vector, 1);
            return Pack(ref value);
        }

        /// <summary>
        /// Packs a <see cref="Vector4"/> into a uint.
        /// </summary>
        /// <param name="vector">The vector containing the values to pack.</param>
        /// <returns>The <see cref="uint"/> containing the packed values.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Pack(ref Vector4 vector)
        {
            vector *= MaxBytes;
            vector += Half;
            vector = Vector4.Clamp(vector, Vector4.Zero, MaxBytes);
            return (uint)(((byte)vector.X << RedShift)
                        | ((byte)vector.Y << GreenShift)
                        | ((byte)vector.Z << BlueShift)
                        | (byte)vector.W << AlphaShift);
        }
    }
}