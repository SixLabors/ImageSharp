// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Color.cs" company="James South">
//   Copyright © James South and contributors.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Represents an BGRA (blue, green, red, alpha) color.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor
{
    using System;
    using System.ComponentModel;
    using System.Globalization;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Represents an BGRA (blue, green, red, alpha) color.
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    public struct Color : IEquatable<Color>
    {
        /// <summary>
        /// Represents a <see cref="Color"/> that has B, G, R, and A values set to zero.
        /// </summary>
        public static readonly Color Empty;

        /// <summary>
        /// Represents a transparent <see cref="Color"/> that has B, G, R, and A values set to 255, 255, 255, 0.
        /// </summary>
        public static readonly Color Transparent = new Color(255, 255, 255, 0);

        /// <summary>
        /// Represents a black <see cref="Color"/> that has B, G, R, and A values set to 0, 0, 0, 0.
        /// </summary>
        public static readonly Color Black = new Color(0, 0, 0, 255);

        /// <summary>
        /// Represents a white <see cref="Color"/> that has B, G, R, and A values set to 255, 255, 255, 255.
        /// </summary>
        public static readonly Color White = new Color(255, 255, 255, 255);

        /// <summary>
        /// Holds the blue component of the color
        /// </summary>
        [FieldOffset(0)]
        public byte B;

        /// <summary>
        /// Holds the green component of the color
        /// </summary>
        [FieldOffset(1)]
        public byte G;

        /// <summary>
        /// Holds the red component of the color
        /// </summary>
        [FieldOffset(2)]
        public byte R;

        /// <summary>
        /// Holds the alpha component of the color
        /// </summary>
        [FieldOffset(3)]
        public byte A;

        /// <summary>
        /// Permits the <see cref="Color"/> to be treated as a 32 bit integer.
        /// </summary>
        [FieldOffset(0)]
        public int Bgra;

        /// <summary>
        /// Initializes a new instance of the <see cref="Color"/> struct.
        /// </summary>
        /// <param name="b">
        /// The blue component of this <see cref="Color"/>.
        /// </param>
        /// <param name="g">
        /// The green component of this <see cref="Color"/>.
        /// </param>
        /// <param name="r">
        /// The red component of this <see cref="Color"/>.
        /// </param>
        public Color(byte b, byte g, byte r)
            : this()
        {
            this.B = b;
            this.G = g;
            this.R = r;
            this.A = 255;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Color"/> struct.
        /// </summary>
        /// <param name="b">
        /// The blue component of this <see cref="Color"/>.
        /// </param>
        /// <param name="g">
        /// The green component of this <see cref="Color"/>.
        /// </param>
        /// <param name="r">
        /// The red component of this <see cref="Color"/>.
        /// </param>
        /// <param name="a">
        /// The alpha component of this <see cref="Color"/>.
        /// </param>
        public Color(byte b, byte g, byte r, byte a)
            : this()
        {
            this.B = b;
            this.G = g;
            this.R = r;
            this.A = a;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Color"/> struct.
        /// </summary>
        /// <param name="bgra">
        /// The combined color components.
        /// </param>
        public Color(int bgra)
            : this()
        {
            this.Bgra = bgra;
        }

        /// <summary>
        /// Gets a value indicating whether this <see cref="Color"/> is empty.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool IsEmpty
        {
            get
            {
                return this.B == 0 && this.G == 0 && this.R == 0 && this.A == 0;
            }
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
            if (obj is Color)
            {
                Color color = (Color)obj;

                return this.Bgra == color.Bgra;
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
            return this.GetHashCode(this);
        }

        /// <summary>
        /// Returns the fully qualified type name of this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="T:System.String"/> containing a fully qualified type name.
        /// </returns>
        public override string ToString()
        {
            return "{B=" + this.B.ToString(CultureInfo.CurrentCulture)
            + ",G=" + this.G.ToString(CultureInfo.CurrentCulture)
            + ",R=" + this.R.ToString(CultureInfo.CurrentCulture)
            + ",A=" + this.A.ToString(CultureInfo.CurrentCulture) + "}";
        }

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <returns>
        /// True if the current object is equal to the <paramref name="other"/> parameter; otherwise, false.
        /// </returns>
        /// <param name="other">An object to compare with this object.</param>
        public bool Equals(Color other)
        {
            return this.B.Equals(other.B) && this.G.Equals(other.G)
                && this.R.Equals(other.R) && this.A.Equals(other.A);
        }

        /// <summary>
        /// Returns the hash code for the given instance.
        /// </summary>
        /// <param name="obj">
        /// The instance of <see cref="Color"/> to return the hash code for.
        /// </param>
        /// <returns>
        /// A 32-bit signed integer that is the hash code for this instance.
        /// </returns>
        private int GetHashCode(Color obj)
        {
            unchecked
            {
                int hashCode = obj.B.GetHashCode();
                hashCode = (hashCode * 397) ^ obj.G.GetHashCode();
                hashCode = (hashCode * 397) ^ obj.R.GetHashCode();
                hashCode = (hashCode * 397) ^ obj.A.GetHashCode();
                return hashCode;
            }
        }
    }
}
