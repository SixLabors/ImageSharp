// <copyright file="Bgra32.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>
namespace ImageProcessorCore
{
    using System;
    using System.Numerics;

    /// <summary>
    /// Packed vector type containing four 8-bit unsigned normalized values ranging from 0 to 1.
    /// </summary>
    public unsafe struct Bgra32 : IPackedVector<uint>, IEquatable<Bgra32>
    {
        const uint B_MASK = 0x000000FF;
        const uint G_MASK = 0x0000FF00;
        const uint R_MASK = 0x00FF0000;
        const uint A_MASK = 0xFF000000;
        const int B_SHIFT = 0;
        const int G_SHIFT = 8;
        const int R_SHIFT = 16;
        const int A_SHIFT = 24;

        private uint packedValue;

        /// <summary>
        /// Initializes a new instance of the <see cref="Bgra32"/> struct. 
        /// </summary>
        /// <param name="b">The blue component.</param>
        /// <param name="g">The green component.</param>
        /// <param name="r">The red component.</param>
        /// <param name="a">The alpha component.</param>
        public Bgra32(float b, float g, float r, float a)
        {
            Vector4 clamped = Vector4.Clamp(new Vector4(b, g, r, a), Vector4.Zero, Vector4.One) * 255f;
            this.packedValue = Pack(ref clamped);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Bgra32"/> struct. 
        /// </summary>
        /// <param name="vector">
        /// The vector containing the components for the packed vector.
        /// </param>
        public Bgra32(Vector4 vector)
        {
            Vector4 clamped = Vector4.Clamp(vector, Vector4.Zero, Vector4.One) * 255f;
            this.packedValue = Pack(ref clamped);
        }

        public byte B
        {
            get
            {
                return (byte)(this.packedValue & B_MASK);
            }

            set
            {
                this.packedValue = (this.packedValue & ~B_MASK) | value;
            }
        }

        public byte G
        {
            get
            {
                return (byte)((this.packedValue & G_MASK) >> G_SHIFT);
            }

            set
            {
                this.packedValue = (this.packedValue & ~G_MASK) | (((uint)value) << G_SHIFT);
            }
        }

        public byte R
        {
            get
            {
                return (byte)((this.packedValue & R_MASK) >> R_SHIFT);
            }

            set
            {
                this.packedValue = (this.packedValue & ~R_MASK) | (((uint)value) << R_SHIFT);
            }
        }

        public byte A
        {
            get
            {
                return (byte)((this.packedValue & A_MASK) >> A_SHIFT);
            }

            set
            {
                this.packedValue = (this.packedValue & ~A_MASK) | (((uint)value) << A_SHIFT);
            }
        }

        /// <inheritdoc/>
        public uint PackedValue()
        {
            return this.packedValue;
        }

        public void Add<TP>(TP value) where TP : IPackedVector<uint>
        {
            // this.PackVector(this.ToVector4() + value.ToVector4());
        }

        public void Subtract<TP>(TP value) where TP : IPackedVector<uint>
        {
            // this.PackVector(this.ToVector4() - value.ToVector4());
        }

        public void Multiply<TP>(TP value) where TP : IPackedVector<uint>
        {
            // this.PackVector(this.ToVector4() * value.ToVector4());
        }

        public void Multiply<TP>(float value) where TP : IPackedVector<uint>
        {
            this.B = (byte)(this.B * value);
            this.G = (byte)(this.G * value);
            this.R = (byte)(this.R * value);
            this.A = (byte)(this.A * value);
        }

        public void Divide<TP>(TP value) where TP : IPackedVector<uint>
        {
            // this.PackVector(this.ToVector4() / value.ToVector4());
        }

        public void Divide<TP>(float value) where TP : IPackedVector<uint>
        {
            this.B = (byte)(this.B / value);
            this.G = (byte)(this.G / value);
            this.R = (byte)(this.R / value);
            this.A = (byte)(this.A / value);
        }

        /// <summary>
        /// Computes the product of multiplying a Bgra32 by a given factor.
        /// </summary>
        /// <param name="value">The Bgra32.</param>
        /// <param name="factor">The multiplication factor.</param>
        /// <returns>
        /// The <see cref="Bgra32"/>
        /// </returns>
        public static Bgra32 operator *(Bgra32 value, float factor)
        {
            byte b = (byte)(value.B * factor);
            byte g = (byte)(value.G * factor);
            byte r = (byte)(value.R * factor);
            byte a = (byte)(value.A * factor);

            return new Bgra32(b, g, r, a);
        }

        /// <summary>
        /// Computes the product of multiplying a Bgra32 by a given factor.
        /// </summary>
        /// <param name="factor">The multiplication factor.</param>
        /// <param name="value">The Bgra32.</param>
        /// <returns>
        /// The <see cref="Bgra32"/>
        /// </returns>
        public static Bgra32 operator *(float factor, Bgra32 value)
        {
            byte b = (byte)(value.B * factor);
            byte g = (byte)(value.G * factor);
            byte r = (byte)(value.R * factor);
            byte a = (byte)(value.A * factor);

            return new Bgra32(b, g, r, a);
        }

        /// <summary>
        /// Computes the product of multiplying two Bgra32s.
        /// </summary>
        /// <param name="left">The Bgra32 on the left hand of the operand.</param>
        /// <param name="right">The Bgra32 on the right hand of the operand.</param>
        /// <returns>
        /// The <see cref="Bgra32"/>
        /// </returns>
        public static Bgra32 operator *(Bgra32 left, Bgra32 right)
        {
            byte b = (byte)(left.B * right.B);
            byte g = (byte)(left.G * right.G);
            byte r = (byte)(left.R * right.R);
            byte a = (byte)(left.A * right.A);

            return new Bgra32(b, g, r, a);
        }

        /// <summary>
        /// Computes the sum of adding two Bgra32s.
        /// </summary>
        /// <param name="left">The Bgra32 on the left hand of the operand.</param>
        /// <param name="right">The Bgra32 on the right hand of the operand.</param>
        /// <returns>
        /// The <see cref="Bgra32"/>
        /// </returns>
        public static Bgra32 operator +(Bgra32 left, Bgra32 right)
        {
            byte b = (byte)(left.B + right.B);
            byte g = (byte)(left.G + right.G);
            byte r = (byte)(left.R + right.R);
            byte a = (byte)(left.A + right.A);

            return new Bgra32(b, g, r, a);
        }

        /// <summary>
        /// Computes the difference left by subtracting one Bgra32 from another.
        /// </summary>
        /// <param name="left">The Bgra32 on the left hand of the operand.</param>
        /// <param name="right">The Bgra32 on the right hand of the operand.</param>
        /// <returns>
        /// The <see cref="Bgra32"/>
        /// </returns>
        public static Bgra32 operator -(Bgra32 left, Bgra32 right)
        {
            byte b = (byte)(left.B - right.B);
            byte g = (byte)(left.G - right.G);
            byte r = (byte)(left.R - right.R);
            byte a = (byte)(left.A - right.A);

            return new Bgra32(b, g, r, a);
        }

        /// <summary>
        /// Compares two <see cref="Bgra32"/> objects for equality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="Bgra32"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="Bgra32"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the current left is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator ==(Bgra32 left, Bgra32 right)
        {
            return left.packedValue == right.packedValue;
        }

        /// <summary>
        /// Compares two <see cref="Bgra32"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="Bgra32"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="Bgra32"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the current left is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator !=(Bgra32 left, Bgra32 right)
        {
            return left.packedValue != right.packedValue;
        }

        /// <inheritdoc/>
        public void PackVector(Vector4 vector)
        {
            Vector4 clamped = Vector4.Clamp(vector, Vector4.Zero, Vector4.One) * 255f;
            this.packedValue = Pack(ref clamped);
        }

        /// <inheritdoc/>
        public void PackBytes(byte x, byte y, byte z, byte w)
        {
            this.packedValue = Pack(ref x, ref y, ref z, ref w);
        }

        /// <inheritdoc/>
        public Vector4 ToVector4()
        {
            return new Vector4(
                this.packedValue & 0xFF,
                (this.packedValue >> 8) & 0xFF,
                (this.packedValue >> 16) & 0xFF,
                (this.packedValue >> 24) & 0xFF) / 255f;
        }

        /// <inheritdoc/>
        public byte[] ToBytes()
        {
            return new[]
            {
                (byte)(this.packedValue & 0xFF),
                (byte)((this.packedValue >> 8) & 0xFF),
                (byte)((this.packedValue >> 16) & 0xFF),
                (byte)((this.packedValue >> 24) & 0xFF)
            };
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return (obj is Bgra32) && this.Equals((Bgra32)obj);
        }

        /// <inheritdoc/>
        public bool Equals(Bgra32 other)
        {
            return this.packedValue == other.packedValue;
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
            return this.GetHashCode(this);
        }

        /// <summary>
        /// Sets the packed representation from the given component values.
        /// </summary>
        /// <param name="vector">
        /// The vector containing the components for the packed vector.
        /// </param>
        /// <returns>
        /// The <see cref="uint"/>.
        /// </returns>
        private static uint Pack(ref Vector4 vector)
        {
            return (uint)Math.Round(vector.X) |
                   ((uint)Math.Round(vector.Y) << 8) |
                   ((uint)Math.Round(vector.Z) << 16) |
                   ((uint)Math.Round(vector.W) << 24);
        }

        private static uint Pack(ref byte x, ref byte y, ref byte z, ref byte w)
        {
            return x | ((uint)y << 8) | ((uint)z << 16) | ((uint)w << 24);
        }

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <param name="packed">
        /// The instance of <see cref="Bgra32"/> to return the hash code for.
        /// </param>
        /// <returns>
        /// A 32-bit signed integer that is the hash code for this instance.
        /// </returns>
        private int GetHashCode(Bgra32 packed)
        {
            return packed.packedValue.GetHashCode();
        }
    }
}
