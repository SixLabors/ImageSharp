// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Hsv.cs" company="James South">
//   Copyright © James South and contributors.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Represents a HSV (hue, saturation, value) color. Also known as HSB (hue, saturation, brightness).
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor
{
    using System;
    using System.ComponentModel;

    /// <summary>
    /// Represents a HSV (hue, saturation, value) color. Also known as HSB (hue, saturation, brightness).
    /// </summary>
    public struct Hsv : IEquatable<Hsv>
    {
        /// <summary>
        /// Represents a <see cref="Hsv"/> that has H, S, and V values set to zero.
        /// </summary>
        public static readonly Hsv Empty = new Hsv();

        /// <summary>
        /// The epsilon for comparing floating point numbers.
        /// </summary>
        private const float Epsilon = 0.0001f;

        /// <summary>
        /// Initializes a new instance of the <see cref="Hsv"/> struct.
        /// </summary>
        /// <param name="h">The h hue component.</param>
        /// <param name="s">The s saturation component.</param>
        /// <param name="v">The v value (brightness) component.</param> 
        public Hsv(float h, float s, float v)
        {
            this.H = h.Clamp(0, 360);
            this.S = s.Clamp(0, 100);
            this.V = v.Clamp(0, 100);
        }

        /// <summary>
        /// Gets the H hue component.
        /// <remarks>A value ranging between 0 and 360.</remarks>
        /// </summary>
        public float H { get; }

        /// <summary>
        /// Gets the S saturation component.
        /// <remarks>A value ranging between 0 and 100.</remarks>
        /// </summary>
        public float S { get; }


        /// <summary>
        /// Gets the V value (brightness) component.
        /// <remarks>A value ranging between 0 and 100.</remarks>
        /// </summary>
        public float V { get; }

        /// <summary>
        /// Gets a value indicating whether this <see cref="Hsv"/> is empty.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool IsEmpty => Math.Abs(this.H) < Epsilon
                               && Math.Abs(this.S) < Epsilon
                               && Math.Abs(this.V) < Epsilon;

        /// <summary>
        /// Compares two <see cref="Hsv"/> objects. The result specifies whether the values
        /// of the <see cref="Hsv.H"/>, <see cref="Hsv.S"/>, and <see cref="Hsv.V"/>
        /// properties of the two <see cref="Hsv"/> objects are equal.
        /// </summary>
        /// <param name="left">
        /// The <see cref="Hsv"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="Hsv"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the current left is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator ==(Hsv left, Hsv right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Compares two <see cref="Hsv"/> objects. The result specifies whether the values
        /// of the <see cref="Hsv.H"/>, <see cref="Hsv.S"/>, and <see cref="Hsv.V"/>
        /// properties of the two <see cref="Hsv"/> objects are unequal.
        /// </summary>
        /// <param name="left">
        /// The <see cref="Hsv"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="Hsv"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the current left is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator !=(Hsv left, Hsv right)
        {
            return !left.Equals(right);
        }

        /// <summary>
        /// Allows the implicit conversion of an instance of <see cref="Bgra"/> to a 
        /// <see cref="Hsv"/>.
        /// </summary>
        /// <param name="color">
        /// The instance of <see cref="Bgra"/> to convert.
        /// </param>
        /// <returns>
        /// An instance of <see cref="Hsv"/>.
        /// </returns>
        public static implicit operator Hsv(Bgra color)
        {
            float max = Math.Max(color.R, Math.Max(color.G, color.B));
            float min = Math.Min(color.R, Math.Min(color.G, color.B));
            float delta = max - min;

            if (Math.Abs(max) < Epsilon)
            {
                return new Hsv(0, 0, 0);
            }

            float h = 0.0F;
            float s;
            float v;

            if (Math.Abs(delta) < Epsilon) { h = 0; }
            else if (Math.Abs(color.R - max) < Epsilon) { h = (color.G - color.B) / delta; }
            else if (Math.Abs(color.G - max) < Epsilon) { h = 2 + (color.B - color.R) / delta; }
            else if (Math.Abs(color.B - max) < Epsilon) { h = 4 + (color.R - color.G) / delta; }

            h *= 60;
            if (h < 0.0) { h += 360; }

            s = delta / max;
            v = max / 255F;
            return new Hsv(h, s, v);
        }

        /// <summary>
        /// Allows the implicit conversion of an instance of <see cref="Hsv"/> to a 
        /// <see cref="Bgra"/>.
        /// </summary>
        /// <param name="color">
        /// The instance of <see cref="Hsv"/> to convert.
        /// </param>
        /// <returns>
        /// An instance of <see cref="Bgra"/>.
        /// </returns>
        public static implicit operator Bgra(Hsv color)
        {
            if (Math.Abs(color.S) < Epsilon)
            {
                byte component = (byte)(color.V * 255);
                return new Bgra(component, component, component, 255);
            }

            float h = (Math.Abs(color.H - 360) < Epsilon) ? 0 : color.H / 60;
            int i = (int)(Math.Truncate(h));
            float f = h - i;

            float p = color.V * (1.0f - color.S);
            float q = color.V * (1.0f - (color.S * f));
            float t = color.V * (1.0f - (color.S * (1.0f - f)));

            float r, g, b;
            switch (i)
            {
                case 0:
                    r = color.V;
                    g = t;
                    b = p;
                    break;

                case 1:
                    r = q;
                    g = color.V;
                    b = p;
                    break;

                case 2:
                    r = p;
                    g = color.V;
                    b = t;
                    break;

                case 3:
                    r = p;
                    g = q;
                    b = color.V;
                    break;

                case 4:
                    r = t;
                    g = p;
                    b = color.V;
                    break;

                default:
                    r = color.V;
                    g = p;
                    b = q;
                    break;
            }

            return new Bgra((byte)b, (byte)g, (byte)r);
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
            if (obj is Hsv)
            {
                Hsv color = (Hsv)obj;

                return Math.Abs(this.H - color.H) < Epsilon
                    && Math.Abs(this.S - color.S) < Epsilon
                    && Math.Abs(this.V - color.V) < Epsilon;
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
                int hashCode = this.H.GetHashCode();
                hashCode = (hashCode * 397) ^ this.S.GetHashCode();
                hashCode = (hashCode * 397) ^ this.V.GetHashCode();
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
                return "Hsv [ Empty ]";
            }

            return $"Hsv [ H={this.H:#0.##}, S={this.S:#0.##}, V={this.V:#0.##} ]";
        }

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <returns>
        /// True if the current object is equal to the <paramref name="other"/> parameter; otherwise, false.
        /// </returns>
        /// <param name="other">An object to compare with this object.</param>
        public bool Equals(Hsv other)
        {
            return this.H.Equals(other.H)
            && this.S.Equals(other.S)
            && this.V.Equals(other.V);
        }
    }
}
