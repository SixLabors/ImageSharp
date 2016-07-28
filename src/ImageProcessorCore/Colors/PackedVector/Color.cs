// <copyright file="Color.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
{
    using System;
    using System.Numerics;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Packed vector type containing four 8-bit unsigned normalized values ranging from 0 to 255.
    /// The color components are stored in red, green, blue, and alpha order.
    /// </summary>
    /// <remarks>
    /// This struct is fully mutable. This is done (against the guidelines) for the sake of performance,
    /// as it avoids the need to create new values for modification operations.
    /// </remarks>
    [StructLayout(LayoutKind.Explicit)]
    public partial struct Color : IPackedVector<uint>, IEquatable<Color>
    {
        /// <summary>
        /// Gets or sets the blue component.
        /// </summary>
        [FieldOffset(0)]
        public byte R;

        /// <summary>
        /// Gets or sets the green component.
        /// </summary>
        [FieldOffset(1)]
        public byte G;

        /// <summary>
        /// Gets or sets the red component.
        /// </summary>
        [FieldOffset(2)]
        public byte B;

        /// <summary>
        /// Gets or sets the alpha component.
        /// </summary>
        [FieldOffset(3)]
        public byte A;

        /// <summary>
        /// The packed value.
        /// </summary>
        [FieldOffset(0)]
        private readonly uint packedValue;

        /// <summary>
        /// Initializes a new instance of the <see cref="Color"/> struct. 
        /// </summary>
        /// <param name="r">The red component.</param>
        /// <param name="g">The green component.</param>
        /// <param name="b">The blue component.</param>
        /// <param name="a">The alpha component.</param>
        public Color(float r, float g, float b, float a)
            : this()
        {
            Vector4 clamped = Vector4.Clamp(new Vector4(r, g, b, a), Vector4.Zero, Vector4.One) * 255F;
            this.R = (byte)Math.Round(clamped.X);
            this.G = (byte)Math.Round(clamped.Y);
            this.B = (byte)Math.Round(clamped.Z);
            this.A = (byte)Math.Round(clamped.W);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Color"/> struct. 
        /// </summary>
        /// <param name="r">The red component.</param>
        /// <param name="g">The green component.</param>
        /// <param name="b">The blue component.</param>
        /// <param name="a">The alpha component.</param>
        public Color(byte r, byte g, byte b, byte a)
            : this()
        {
            this.R = r;
            this.G = g;
            this.B = b;
            this.A = a;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Color"/> struct. 
        /// </summary>
        /// <param name="vector">
        /// The vector containing the components for the packed vector.
        /// </param>
        public Color(Vector4 vector)
            : this()
        {
            Vector4 clamped = Vector4.Clamp(vector, Vector4.Zero, Vector4.One) * 255F;
            this.B = (byte)Math.Round(clamped.X);
            this.G = (byte)Math.Round(clamped.Y);
            this.R = (byte)Math.Round(clamped.Z);
            this.A = (byte)Math.Round(clamped.W);
        }

        /// <summary>
        /// Compares two <see cref="Color"/> objects for equality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="Color"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="Color"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the current left is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator ==(Color left, Color right)
        {
            return left.packedValue == right.packedValue;
        }

        /// <summary>
        /// Compares two <see cref="Color"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="Color"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="Color"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the current left is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator !=(Color left, Color right)
        {
            return left.packedValue != right.packedValue;
        }

        /// <inheritdoc/>
        public uint PackedValue()
        {
            return this.packedValue;
        }

        /// <inheritdoc/>
        public void PackVector(Vector4 vector)
        {
            Vector4 clamped = Vector4.Clamp(vector, Vector4.Zero, Vector4.One) * 255F;
            this.R = (byte)Math.Round(clamped.X);
            this.G = (byte)Math.Round(clamped.Y);
            this.B = (byte)Math.Round(clamped.Z);
            this.A = (byte)Math.Round(clamped.W);
        }

        /// <inheritdoc/>
        public void PackBytes(byte x, byte y, byte z, byte w)
        {
            this.R = x;
            this.G = y;
            this.B = z;
            this.A = w;
        }

        /// <inheritdoc/>
        public Vector4 ToVector4()
        {
            return new Vector4(this.R, this.G, this.B, this.A) / 255F;
        }

        /// <inheritdoc/>
        public byte[] ToBytes()
        {
            return new[] { this.R, this.G, this.B, this.A };
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return (obj is Color) && this.Equals((Color)obj);
        }

        /// <inheritdoc/>
        public bool Equals(Color other)
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
        /// Returns the hash code for this instance.
        /// </summary>
        /// <param name="packed">
        /// The instance of <see cref="Color"/> to return the hash code for.
        /// </param>
        /// <returns>
        /// A 32-bit signed integer that is the hash code for this instance.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetHashCode(Color packed)
        {
            return packed.packedValue.GetHashCode();
        }
    }
}