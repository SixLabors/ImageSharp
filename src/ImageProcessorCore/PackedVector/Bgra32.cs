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
    public struct Bgra32 : IPackedVector<uint>, IEquatable<Bgra32>
    {
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
            this.PackedValue = Pack(ref clamped);
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
            this.PackedValue = Pack(ref clamped);
        }

        /// <inheritdoc/>
        public uint PackedValue { get; set; }

        // The maths are wrong here I just wanted to test the performance to see if the
        // issues are caused by the Vector transform or something else.
        public void Add(IPackedVector<uint> value)
        {
 
        }

        public void Subtract(IPackedVector value)
        {
            
        }

        public void Multiply(IPackedVector value)
        {

        }

        public void Multiply(float value)
        {

        }

        public void Divide(IPackedVector value)
        {
         
        }

        public void Divide(float value)
        {
        
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
            return left.PackedValue == right.PackedValue;
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
            return left.PackedValue != right.PackedValue;
        }

        /// <inheritdoc/>
        public void PackVector(Vector4 vector)
        {
            Vector4 clamped = Vector4.Clamp(vector, Vector4.Zero, Vector4.One) * 255f;
            this.PackedValue = Pack(ref clamped);
        }

        /// <inheritdoc/>
        public void PackBytes(byte x, byte y, byte z, byte w)
        {
            Vector4 vector = new Vector4(x, y, z, w);
            this.PackedValue = Pack(ref vector);
        }

        /// <inheritdoc/>
        public Vector4 ToVector4()
        {
            return new Vector4(
                this.PackedValue & 0xFF,
                (this.PackedValue >> 8) & 0xFF,
                (this.PackedValue >> 16) & 0xFF,
                (this.PackedValue >> 24) & 0xFF) / 255f;
        }

        /// <inheritdoc/>
        public byte[] ToBytes()
        {
            return new[]
            {
                (byte)(this.PackedValue & 0xFF),
                (byte)((this.PackedValue >> 8) & 0xFF),
                (byte)((this.PackedValue >> 16) & 0xFF),
                (byte)((this.PackedValue >> 24) & 0xFF)
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
            return this.PackedValue == other.PackedValue;
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
            return packed.PackedValue.GetHashCode();
        }
    }
}
