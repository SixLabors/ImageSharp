//// <copyright file="Bgra32.cs" company="James Jackson-South">
//// Copyright (c) James Jackson-South and contributors.
//// Licensed under the Apache License, Version 2.0.
//// </copyright>

//namespace ImageProcessorCore
//{
//    using System;
//    using System.ComponentModel;
//    using System.Numerics;

//    /// <summary>
//    /// Represents an BGRA (blue, green, red, alpha) color.
//    /// </summary>
//    public struct Bgra32 : IEquatable<Bgra32>
//    {
//        /// <summary>
//        /// Represents a 32 bit <see cref="Bgra32"/> that has B, G, R, and A values set to zero.
//        /// </summary>
//        public static readonly Bgra32 Empty = default(Bgra32);

//        /// <summary>
//        /// The backing vector for SIMD support.
//        /// </summary>
//        private Vector4 backingVector;

//        /// <summary>
//        /// Initializes a new instance of the <see cref="Bgra32"/> struct.
//        /// </summary>
//        /// <param name="b">The blue component of this <see cref="Bgra32"/>.</param>
//        /// <param name="g">The green component of this <see cref="Bgra32"/>.</param>
//        /// <param name="r">The red component of this <see cref="Bgra32"/>.</param>
//        /// <param name="a">The alpha component of this <see cref="Bgra32"/>.</param>
//        public Bgra32(byte b, byte g, byte r, byte a = 255)
//            : this()
//        {
//            this.backingVector = Vector4.Clamp(new Vector4(b, g, r, a), Vector4.Zero, new Vector4(255));
//        }

//        /// <summary>
//        /// Gets the blue component of the color
//        /// </summary>
//        public byte B => (byte)this.backingVector.X;

//        /// <summary>
//        /// Gets the green component of the color
//        /// </summary>
//        public byte G => (byte)this.backingVector.Y;

//        /// <summary>
//        /// Gets the red component of the color
//        /// </summary>
//        public byte R => (byte)this.backingVector.Z;

//        /// <summary>
//        /// Gets the alpha component of the color
//        /// </summary>
//        public byte A => (byte)this.backingVector.W;

//        /// <summary>
//        /// Gets the <see cref="Bgra32"/> integer representation of the color.
//        /// </summary>
//        public int Bgra => (this.R << 16) | (this.G << 8) | (this.B << 0) | (this.A << 24);

//        /// <summary>
//        /// Gets a value indicating whether this <see cref="Bgra32"/> is empty.
//        /// </summary>
//        [EditorBrowsable(EditorBrowsableState.Never)]
//        public bool IsEmpty => this.Equals(Empty);

//        /// <summary>
//        /// Allows the implicit conversion of an instance of <see cref="Color"/> to a
//        /// <see cref="Bgra32"/>.
//        /// </summary>
//        /// <param name="color">
//        /// The instance of <see cref="Color"/> to convert.
//        /// </param>
//        /// <returns>
//        /// An instance of <see cref="Bgra32"/>.
//        /// </returns>
//        public static implicit operator Bgra32(Color color)
//        {
//            color = color.Limited * 255f;
//            return new Bgra32((byte)color.B, (byte)color.G, (byte)color.R, (byte)color.A);
//        }

//        /// <summary>
//        /// Compares two <see cref="Bgra32"/> objects for equality.
//        /// </summary>
//        /// <param name="left">
//        /// The <see cref="Bgra32"/> on the left side of the operand.
//        /// </param>
//        /// <param name="right">
//        /// The <see cref="Bgra32"/> on the right side of the operand.
//        /// </param>
//        /// <returns>
//        /// True if the current left is equal to the <paramref name="right"/> parameter; otherwise, false.
//        /// </returns>
//        public static bool operator ==(Bgra32 left, Bgra32 right)
//        {
//            return left.Equals(right);
//        }

//        /// <summary>
//        /// Compares two <see cref="Bgra32"/> objects for inequality.
//        /// </summary>
//        /// <param name="left">
//        /// The <see cref="Bgra32"/> on the left side of the operand.
//        /// </param>
//        /// <param name="right">
//        /// The <see cref="Bgra32"/> on the right side of the operand.
//        /// </param>
//        /// <returns>
//        /// True if the current left is unequal to the <paramref name="right"/> parameter; otherwise, false.
//        /// </returns>
//        public static bool operator !=(Bgra32 left, Bgra32 right)
//        {
//            return !left.Equals(right);
//        }

//        /// <inheritdoc/>
//        public override bool Equals(object obj)
//        {
//            if (obj is Bgra32)
//            {
//                Bgra32 color = (Bgra32)obj;

//                return this.backingVector == color.backingVector;
//            }

//            return false;
//        }

//        /// <inheritdoc/>
//        public override int GetHashCode()
//        {
//            return GetHashCode(this);
//        }

//        /// <inheritdoc/>
//        public override string ToString()
//        {
//            if (this.IsEmpty)
//            {
//                return "Bgra32 [ Empty ]";
//            }

//            return $"Bgra32 [ B={this.B}, G={this.G}, R={this.R}, A={this.A} ]";
//        }

//        /// <inheritdoc/>
//        public bool Equals(Bgra32 other)
//        {
//            return this.backingVector.Equals(other.backingVector);
//        }

//        /// <summary>
//        /// Returns the hash code for this instance.
//        /// </summary>
//        /// <param name="color">
//        /// The instance of <see cref="Cmyk"/> to return the hash code for.
//        /// </param>
//        /// <returns>
//        /// A 32-bit signed integer that is the hash code for this instance.
//        /// </returns>
//        private static int GetHashCode(Bgra32 color) => color.backingVector.GetHashCode();
//    }
//}
