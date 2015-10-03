// <copyright file="Bgra.cs" company="James South">
// Copyright © James South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor
{
    using System;
    using System.ComponentModel;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Represents an BGRA (blue, green, red, alpha) color.
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    public struct Bgra : IEquatable<Bgra>
    {
        /// <summary>
        /// Represents a <see cref="Bgra"/> that has B, G, R, and A values set to zero.
        /// </summary>
        public static readonly Bgra Empty;

        /// <summary>
        /// Represents a transparent <see cref="Bgra"/> that has B, G, R, and A values set to 255, 255, 255, 0.
        /// </summary>
        public static readonly Bgra Transparent = new Bgra(255, 255, 255, 0);

        /// <summary>
        /// Represents a black <see cref="Bgra"/> that has B, G, R, and A values set to 0, 0, 0, 0.
        /// </summary>
        public static readonly Bgra Black = new Bgra(0, 0, 0, 255);

        /// <summary>
        /// Represents a white <see cref="Bgra"/> that has B, G, R, and A values set to 255, 255, 255, 255.
        /// </summary>
        public static readonly Bgra White = new Bgra(255, 255, 255, 255);

        /// <summary>
        /// Holds the blue component of the color
        /// </summary>
        [FieldOffset(0)]
        public readonly byte B;

        /// <summary>
        /// Holds the green component of the color
        /// </summary>
        [FieldOffset(1)]
        public readonly byte G;

        /// <summary>
        /// Holds the red component of the color
        /// </summary>
        [FieldOffset(2)]
        public readonly byte R;

        /// <summary>
        /// Holds the alpha component of the color
        /// </summary>
        [FieldOffset(3)]
        public readonly byte A;

        /// <summary>
        /// Permits the <see cref="Bgra"/> to be treated as a 32 bit integer.
        /// </summary>
        [FieldOffset(0)]
        public readonly int BGRA;

        /// <summary>
        /// Initializes a new instance of the <see cref="Bgra"/> struct.
        /// </summary>
        /// <param name="b">
        /// The blue component of this <see cref="Bgra"/>.
        /// </param>
        /// <param name="g">
        /// The green component of this <see cref="Bgra"/>.
        /// </param>
        /// <param name="r">
        /// The red component of this <see cref="Bgra"/>.
        /// </param>
        public Bgra(byte b, byte g, byte r)
            : this()
        {
            this.B = b;
            this.G = g;
            this.R = r;
            this.A = 255;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Bgra"/> struct.
        /// </summary>
        /// <param name="b">
        /// The blue component of this <see cref="Bgra"/>.
        /// </param>
        /// <param name="g">
        /// The green component of this <see cref="Bgra"/>.
        /// </param>
        /// <param name="r">
        /// The red component of this <see cref="Bgra"/>.
        /// </param>
        /// <param name="a">
        /// The alpha component of this <see cref="Bgra"/>.
        /// </param>
        public Bgra(byte b, byte g, byte r, byte a)
            : this()
        {
            this.B = b;
            this.G = g;
            this.R = r;
            this.A = a;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Bgra"/> struct.
        /// </summary>
        /// <param name="bgra">
        /// The combined color components.
        /// </param>
        public Bgra(int bgra)
            : this()
        {
            this.BGRA = bgra;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Bgra"/> struct.
        /// </summary>
        /// <param name="hex">
        /// The hexadecimal representation of the combined color components arranged
        /// in rgb, rrggbb, or aarrggbb format to match web syntax.
        /// </param>
        public Bgra(string hex)
            : this()
        {
            // Hexadecimal representations are layed out AARRGGBB to we need to do some reordering.
            hex = hex.StartsWith("#") ? hex.Substring(1) : hex;

            if (hex.Length != 8 && hex.Length != 6 && hex.Length != 3)
            {
                throw new ArgumentException("Hexadecimal string is not in the correct format.", nameof(hex));
            }

            if (hex.Length == 8)
            {
                this.B = Convert.ToByte(hex.Substring(6, 2), 16);
                this.G = Convert.ToByte(hex.Substring(4, 2), 16);
                this.R = Convert.ToByte(hex.Substring(2, 2), 16);
                this.A = Convert.ToByte(hex.Substring(0, 2), 16);
            }
            else if (hex.Length == 6)
            {
                this.B = Convert.ToByte(hex.Substring(4, 2), 16);
                this.G = Convert.ToByte(hex.Substring(2, 2), 16);
                this.R = Convert.ToByte(hex.Substring(0, 2), 16);
                this.A = 255;
            }
            else
            {
                string b = char.ToString(hex[2]);
                string g = char.ToString(hex[1]);
                string r = char.ToString(hex[0]);

                this.B = Convert.ToByte(b + b, 16);
                this.G = Convert.ToByte(g + g, 16);
                this.R = Convert.ToByte(r + r, 16);
                this.A = 255;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this <see cref="Bgra"/> is empty.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool IsEmpty => this.B == 0 && this.G == 0 && this.R == 0 && this.A == 0;

        /// <summary>
        /// Compares two <see cref="Bgra"/> objects. The result specifies whether the values
        /// of the <see cref="Bgra.B"/>, <see cref="Bgra.G"/>, <see cref="Bgra.R"/>, and <see cref="Bgra.A"/>
        /// properties of the two <see cref="Bgra"/> objects are equal.
        /// </summary>
        /// <param name="left">
        /// The <see cref="Bgra"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="Bgra"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the current left is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator ==(Bgra left, Bgra right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Compares two <see cref="Bgra"/> objects. The result specifies whether the values
        /// of the <see cref="Bgra.B"/>, <see cref="Bgra.G"/>, <see cref="Bgra.R"/>, and <see cref="Bgra.A"/>
        /// properties of the two <see cref="Bgra"/> objects are unequal.
        /// </summary>
        /// <param name="left">
        /// The <see cref="Bgra"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="Bgra"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the current left is unequal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator !=(Bgra left, Bgra right)
        {
            return !left.Equals(right);
        }

        /// <summary>
        /// Indicates whether this instance and a specified object are equal.
        /// </summary>
        /// <returns>
        /// true if <paramref name="obj"/> and this instance are the same type and represent the same value; otherwise, false.
        /// </returns>
        /// <param name="obj">Another object to compare to. </param>
        public override bool Equals(object obj)
        {
            if (obj is Bgra)
            {
                Bgra color = (Bgra)obj;

                return this.BGRA == color.BGRA;
            }

            return false;
        }

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <returns>
        /// A 32-bit signed integer that is the hash code for this instance.
        /// </returns>
        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = this.B.GetHashCode();
                hashCode = (hashCode * 397) ^ this.G.GetHashCode();
                hashCode = (hashCode * 397) ^ this.R.GetHashCode();
                hashCode = (hashCode * 397) ^ this.A.GetHashCode();
                return hashCode;
            }
        }

        /// <summary>
        /// Returns the fully qualified type name of this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="T:System.String"/> containing a fully qualified type name.
        /// </returns>
        public override string ToString()
        {
            if (this.IsEmpty)
            {
                return "Color [ Empty ]";
            }

            return $"Color [ B={this.B}, G={this.G}, R={this.R}, A={this.A} ]";
        }

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <returns>
        /// True if the current object is equal to the <paramref name="other"/> parameter; otherwise, false.
        /// </returns>
        /// <param name="other">An object to compare with this object.</param>
        public bool Equals(Bgra other)
        {
            return this.BGRA == other.BGRA;
        }
    }
}
