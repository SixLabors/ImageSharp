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
        /// Initializes a new instance of the <see cref="Color"/> struct.
        /// </summary>
        /// <param name="hex">
        /// The hexadecimal representation of the combined color components arranged 
        /// in r,g,b or a,r,g,b format to match web syntax.
        /// </param>
        public Color(string hex)
            : this()
        {
            hex = hex.StartsWith("#") ? hex.Substring(1) : hex;

            if (hex.Length != 8 && hex.Length != 6 && hex.Length != 3)
            {
                throw new ArgumentException("Hexadecimal string is not in the correct format.", "hex");
            }

            if (hex.Length == 8)
            {
                this.R = Convert.ToByte(hex.Substring(2, 2), 16);
                this.G = Convert.ToByte(hex.Substring(4, 2), 16);
                this.B = Convert.ToByte(hex.Substring(6, 2), 16);
                this.A = Convert.ToByte(hex.Substring(0, 2), 16);
            }
            else if (hex.Length == 6)
            {
                this.R = Convert.ToByte(hex.Substring(0, 2), 16);
                this.G = Convert.ToByte(hex.Substring(2, 2), 16);
                this.B = Convert.ToByte(hex.Substring(4, 2), 16);
                this.A = 255;
            }
            else
            {
                string r = char.ToString(hex[0]);
                string g = char.ToString(hex[1]);
                string b = char.ToString(hex[2]);

                this.R = Convert.ToByte(r + r, 16);
                this.G = Convert.ToByte(g + g, 16);
                this.B = Convert.ToByte(b + b, 16);
                this.A = 255;
            }
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
        /// Compares two <see cref="Color"/> objects. The result specifies whether the values
        /// of the <see cref="Color.B"/>, <see cref="Color.G"/>, <see cref="Color.R"/>, and <see cref="Color.A"/>
        /// properties of the two <see cref="Color"/> objects are equal.
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
            return left.Equals(right);
        }

        /// <summary>
        /// Compares two <see cref="Color"/> objects. The result specifies whether the values
        /// of the <see cref="Color.B"/>, <see cref="Color.G"/>, <see cref="Color.R"/>, and <see cref="Color.A"/>
        /// properties of the two <see cref="Color"/> objects are unequal.
        /// </summary>
        /// <param name="left">
        /// The <see cref="Color"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="Color"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the current left is unequal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator !=(Color left, Color right)
        {
            return !left.Equals(right);
        }

        /// <summary>
        /// Allows the implicit conversion of an instance of <see cref="YCbCrColor"/> to a 
        /// <see cref="Color"/>.
        /// </summary>
        /// <param name="color">
        /// The instance of <see cref="YCbCrColor"/> to convert.
        /// </param>
        /// <returns>
        /// An instance of <see cref="Color"/>.
        /// </returns>
        public static implicit operator Color(YCbCrColor color)
        {
            float y = color.Y;
            float cb = color.Cb - 128;
            float cr = color.Cr - 128;

            byte r = Convert.ToByte((y + (1.402 * cr)).Clamp(0, 255));
            byte g = Convert.ToByte((y - (0.34414 * cb) - (0.71414 * cr)).Clamp(0, 255));
            byte b = Convert.ToByte((y + (1.772 * cb)).Clamp(0, 255));

            return new Color(b, g, r, 255);
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
            if (this.IsEmpty)
            {
                return "Color [Empty]";
            }

            return string.Format("Color [ B={0}, G={1}, R={2}, A={3} ]", this.B, this.G, this.R, this.A);
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
        /// <param name="color">
        /// The instance of <see cref="Color"/> to return the hash code for.
        /// </param>
        /// <returns>
        /// A 32-bit signed integer that is the hash code for this instance.
        /// </returns>
        private int GetHashCode(Color color)
        {
            unchecked
            {
                int hashCode = color.B.GetHashCode();
                hashCode = (hashCode * 397) ^ color.G.GetHashCode();
                hashCode = (hashCode * 397) ^ color.R.GetHashCode();
                hashCode = (hashCode * 397) ^ color.A.GetHashCode();
                return hashCode;
            }
        }
    }
}
