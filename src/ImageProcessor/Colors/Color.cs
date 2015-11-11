// <copyright file="Color.cs" company="James South">
// Copyright (c) James South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor
{
    using System;
    using System.ComponentModel;
    using System.Numerics;

    /// <summary>
    /// Represents a four-component color using red, green, blue, and alpha data.
    /// </summary>
    /// <remarks>
    /// This struct is fully mutable. This is done (against the guidelines) for the sake of performance,
    /// as it avoids the need to create new values for modification operations.
    /// </remarks>
    public struct Color : IEquatable<Color>
    {
        /// <summary>
        /// Represents a <see cref="Color"/> that has R, G, B, and A values set to zero.
        /// </summary>
        public static readonly Color Empty = default(Color);

        /// <summary>
        /// The epsilon for comparing floating point numbers.
        /// </summary>
        private const float Epsilon = 0.0001f;

        /// <summary>
        /// The backing vector for SIMD support.
        /// </summary>
        private Vector4 backingVector;

        /// <summary>
        /// Initializes a new instance of the <see cref="Color"/> struct with the alpha component set to 1.
        /// </summary>
        /// <param name="r">The red component of this <see cref="Color"/>.</param>
        /// <param name="g">The green component of this <see cref="Color"/>.</param>
        /// <param name="b">The blue component of this <see cref="Color"/>.</param>
        public Color(float r, float g, float b)
            : this(r, g, b, 1)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Color"/> struct.
        /// </summary>
        /// <param name="r">The red component of this <see cref="Color"/>.</param>
        /// <param name="g">The green component of this <see cref="Color"/>.</param>
        /// <param name="b">The blue component of this <see cref="Color"/>.</param>
        /// <param name="a">The alpha component of this <see cref="Color"/>.</param>
        public Color(float r, float g, float b, float a)
            : this()
        {
            this.backingVector.X = r;
            this.backingVector.Y = g;
            this.backingVector.Z = b;
            this.backingVector.W = a;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Color"/> struct.
        /// </summary>
        /// <param name="hex">
        /// The hexadecimal representation of the combined color components arranged
        /// in rgb, rrggbb, or aarrggbb format to match web syntax.
        /// </param>
        public Color(string hex)
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
                this.R = Convert.ToByte(hex.Substring(2, 2), 16) / 255f;
                this.G = Convert.ToByte(hex.Substring(4, 2), 16) / 255f;
                this.B = Convert.ToByte(hex.Substring(6, 2), 16) / 255f;
                this.A = Convert.ToByte(hex.Substring(0, 2), 16) / 255f;
            }
            else if (hex.Length == 6)
            {
                this.R = Convert.ToByte(hex.Substring(0, 2), 16) / 255f;
                this.G = Convert.ToByte(hex.Substring(2, 2), 16) / 255f;
                this.B = Convert.ToByte(hex.Substring(4, 2), 16) / 255f;
                this.A = 1;
            }
            else
            {
                string r = char.ToString(hex[0]);
                string g = char.ToString(hex[1]);
                string b = char.ToString(hex[2]);

                this.B = Convert.ToByte(b + b, 16) / 255f;
                this.G = Convert.ToByte(g + g, 16) / 255f;
                this.R = Convert.ToByte(r + r, 16) / 255f;
                this.A = 1;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Color"/> struct.
        /// </summary>
        /// <param name="vector">
        /// The vector.
        /// </param>
        public Color(Vector4 vector)
        {
            this.backingVector = vector;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Color"/> struct.
        /// </summary>
        /// <param name="vector">
        /// The vector representing the red, green, and blue componenets.
        /// </param>
        public Color(Vector3 vector)
        {
            this.backingVector = new Vector4(vector.X, vector.Y, vector.Z, 1);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Color"/> struct.
        /// </summary>
        /// <param name="vector">
        /// The vector representing the red, green, and blue componenets.
        /// </param>
        /// <param name="alpha">The alpha component.</param>
        public Color(Vector3 vector, float alpha)
        {
            this.backingVector = new Vector4(vector.X, vector.Y, vector.Z, alpha);
        }

        /// <summary>
        /// Gets or sets the red component of the color.
        /// </summary>
        public float R
        {
            get
            {
                return this.backingVector.X;
            }

            set
            {
                this.backingVector.X = value;
            }
        }

        /// <summary>
        /// Gets or sets the green component of the color.
        /// </summary>
        public float G
        {
            get
            {
                return this.backingVector.Y;
            }

            set
            {
                this.backingVector.Y = value;
            }
        }

        /// <summary>
        /// Gets or sets the blue component of the color.
        /// </summary>
        public float B
        {
            get
            {
                return this.backingVector.Z;
            }

            set
            {
                this.backingVector.Z = value;
            }
        }

        /// <summary>
        /// Gets or sets the alpha component of the color.
        /// </summary>
        public float A
        {
            get
            {
                return this.backingVector.W;
            }

            set
            {
                this.backingVector.W = value;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this <see cref="Color"/> is empty.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool IsEmpty => this.backingVector.Equals(default(Vector4));

        /// <summary>
        /// Gets this color with the component values clamped from 0 to 1.
        /// </summary>
        public Color Limited
        {
            get
            {
                float r = this.R.Clamp(0, 1);
                float g = this.G.Clamp(0, 1);
                float b = this.B.Clamp(0, 1);
                float a = this.A.Clamp(0, 1);
                return new Color(r, g, b, a);
            }
        }

        /// <summary>
        /// Allows the implicit conversion of an instance of <see cref="Color"/> to a
        /// <see cref="Bgra32"/>.
        /// </summary>
        /// <param name="color">The instance of <see cref="Color"/> to convert.</param>
        /// <returns>
        /// An instance of <see cref="Bgra32"/>.
        /// </returns>
        public static implicit operator Color(Bgra32 color)
        {
            return new Color(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);
        }

        /// <summary>
        /// Allows the implicit conversion of an instance of <see cref="Cmyk"/> to a
        /// <see cref="Color"/>.
        /// </summary>
        /// <param name="cmykColor">The instance of <see cref="Cmyk"/> to convert.</param>
        /// <returns>
        /// An instance of <see cref="Color"/>.
        /// </returns>
        public static implicit operator Color(Cmyk cmykColor)
        {
            float r = (1 - cmykColor.C) * (1 - cmykColor.K);
            float g = (1 - cmykColor.M) * (1 - cmykColor.K);
            float b = (1 - cmykColor.Y) * (1 - cmykColor.K);
            return new Color(r, g, b);
        }

        /// <summary>
        /// Allows the implicit conversion of an instance of <see cref="YCbCr"/> to a
        /// <see cref="Color"/>.
        /// </summary>
        /// <param name="color">The instance of <see cref="YCbCr"/> to convert.</param>
        /// <returns>
        /// An instance of <see cref="Color"/>.
        /// </returns>
        public static implicit operator Color(YCbCr color)
        {
            float y = color.Y;
            float cb = color.Cb - 128;
            float cr = color.Cr - 128;

            float r = (float)(y + (1.402 * cr)) / 255f;
            float g = (float)(y - (0.34414 * cb) - (0.71414 * cr)) / 255f;
            float b = (float)(y + (1.772 * cb)) / 255f;

            return new Color(r, g, b);
        }

        /// <summary>
        /// Allows the implicit conversion of an instance of <see cref="Hsv"/> to a
        /// <see cref="Color"/>.
        /// </summary>
        /// <param name="color">The instance of <see cref="Hsv"/> to convert.</param>
        /// <returns>
        /// An instance of <see cref="Color"/>.
        /// </returns>
        public static implicit operator Color(Hsv color)
        {
            float s = color.S;
            float v = color.V;

            if (Math.Abs(s) < Epsilon)
            {
                return new Color(v, v, v, 1);
            }

            float h = (Math.Abs(color.H - 360) < Epsilon) ? 0 : color.H / 60;
            int i = (int)Math.Truncate(h);
            float f = h - i;

            float p = v * (1.0f - s);
            float q = v * (1.0f - (s * f));
            float t = v * (1.0f - (s * (1.0f - f)));

            float r, g, b;
            switch (i)
            {
                case 0:
                    r = v;
                    g = t;
                    b = p;
                    break;

                case 1:
                    r = q;
                    g = v;
                    b = p;
                    break;

                case 2:
                    r = p;
                    g = v;
                    b = t;
                    break;

                case 3:
                    r = p;
                    g = q;
                    b = v;
                    break;

                case 4:
                    r = t;
                    g = p;
                    b = v;
                    break;

                default:
                    r = v;
                    g = p;
                    b = q;
                    break;
            }

            return new Color(r, g, b);
        }

        /// <summary>
        /// Computes the product of multiplying a color by a given factor.
        /// </summary>
        /// <param name="color">The color.</param>
        /// <param name="factor">The multiplication factor.</param>
        /// <returns>
        /// The <see cref="Color"/>
        /// </returns>
        public static Color operator *(Color color, float factor)
        {
            return new Color(color.backingVector * factor);
        }

        /// <summary>
        /// Computes the product of multiplying a color by a given factor.
        /// </summary>
        /// <param name="factor">The multiplication factor.</param>
        /// <param name="color">The color.</param>
        /// <returns>
        /// The <see cref="Color"/>
        /// </returns>
        public static Color operator *(float factor, Color color)
        {
            return new Color(color.backingVector * factor);
        }

        /// <summary>
        /// Computes the product of multiplying two colors.
        /// </summary>
        /// <param name="left">The color on the left hand of the operand.</param>
        /// <param name="right">The color on the right hand of the operand.</param>
        /// <returns>
        /// The <see cref="Color"/>
        /// </returns>
        public static Color operator *(Color left, Color right)
        {
            return new Color(left.backingVector * right.backingVector);
        }

        /// <summary>
        /// Computes the sum of adding two colors.
        /// </summary>
        /// <param name="left">The color on the left hand of the operand.</param>
        /// <param name="right">The color on the right hand of the operand.</param>
        /// <returns>
        /// The <see cref="Color"/>
        /// </returns>
        public static Color operator +(Color left, Color right)
        {
            return new Color(left.R + right.R, left.G + right.G, left.B + right.B, left.A + right.A);
        }

        /// <summary>
        /// Computes the difference left by subtracting one color from another.
        /// </summary>
        /// <param name="left">The color on the left hand of the operand.</param>
        /// <param name="right">The color on the right hand of the operand.</param>
        /// <returns>
        /// The <see cref="Color"/>
        /// </returns>
        public static Color operator -(Color left, Color right)
        {
            return new Color(left.R - right.R, left.G - right.G, left.B - right.B, left.A - right.A);
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
            return left.Equals(right);
        }

        /// <summary>
        /// Compares two <see cref="Hsv"/> objects for inequality.
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
        /// Returns a new color whose components are the average of the components of first and second.
        /// </summary>
        /// <param name="first">The first color.</param>
        /// <param name="second">The second color.</param>
        /// <returns>
        /// The <see cref="Color"/>
        /// </returns>
        public static Color Average(Color first, Color second)
        {
            return new Color((first.backingVector + second.backingVector) * .5f);
        }

        /// <summary>
        /// Linearly interpolates from one color to another based on the given amount.
        /// </summary>
        /// <param name="from">The first color value.</param>
        /// <param name="to">The second color value.</param>
        /// <param name="amount">
        /// The weight value. At amount = 0, "from" is returned, at amount = 1, "to" is returned.
        /// </param>
        /// <returns>
        /// The <see cref="Color"/>
        /// </returns>
        public static Color Lerp(Color from, Color to, float amount)
        {
            amount = amount.Clamp(0f, 1f);

            return (from * (1 - amount)) + (to * amount);
        }

        /// <summary>
        /// Compresseses a linear color signal to its sRGB equivalent.
        /// <see href="http://www.4p8.com/eric.brasseur/gamma.html#formulas"/>
        /// <see href="http://entropymine.com/imageworsener/srgbformula/"/>
        /// </summary>
        /// <param name="linear">The <see cref="Color"/> whos signal to compress.</param>
        /// <returns>The <see cref="Color"/>.</returns>
        public static Color Compand(Color linear)
        {
            // TODO: Is there a faster way to do this?
            float r = Compand(linear.R);
            float g = Compand(linear.G);
            float b = Compand(linear.B);

            return new Color(r, g, b, linear.A);
        }

        /// <summary>
        /// Expands an sRGB color signal to its linear equivalent.
        /// <see href="http://www.4p8.com/eric.brasseur/gamma.html#formulas"/>
        /// <see href="http://entropymine.com/imageworsener/srgbformula/"/>
        /// </summary>
        /// <param name="gamma">The <see cref="Color"/> whos signal to expand.</param>
        /// <returns>The <see cref="Color"/>.</returns>
        public static Color InverseCompand(Color gamma)
        {
            // TODO: Is there a faster way to do this?
            float r = InverseCompand(gamma.R);
            float g = InverseCompand(gamma.G);
            float b = InverseCompand(gamma.B);

            return new Color(r, g, b, gamma.A);
        }

        /// <summary>
        /// Converts a non-premultipled alpha <see cref="Color"/> to a <see cref="Color"/>
        /// that contains premultiplied alpha.
        /// </summary>
        /// <param name="r">The red component of this <see cref="Color"/>.</param>
        /// <param name="g">The green component of this <see cref="Color"/>.</param>
        /// <param name="b">The blue component of this <see cref="Color"/>.</param>
        /// <param name="a">The alpha component of this <see cref="Color"/>.</param>
        /// <returns>The <see cref="Color"/>.</returns>
        public static Color FromNonPremultiplied(float r, float g, float b, float a)
        {
            return new Color(r * a, g * a, b * a, a);
        }

        /// <summary>
        /// Converts a premultipled alpha <see cref="Color"/> to a <see cref="Color"/>
        /// that contains non-premultiplied alpha.
        /// </summary>
        /// <param name="r">The red component of this <see cref="Color"/>.</param>
        /// <param name="g">The green component of this <see cref="Color"/>.</param>
        /// <param name="b">The blue component of this <see cref="Color"/>.</param>
        /// <param name="a">The alpha component of this <see cref="Color"/>.</param>
        /// <returns>The <see cref="Color"/>.</returns>
        public static Color ToNonPremultiplied(float r, float g, float b, float a)
        {
            return new Color(r / a, g / a, b / a, a);
        }

        /// <summary>
        /// Gets a <see cref="Vector4"/> representation for this <see cref="Color"/>.
        /// </summary>
        /// <returns>A <see cref="Vector4"/> representation for this object.</returns>
        public Vector4 ToVector4()
        {
            return new Vector4(this.R, this.G, this.B, this.A);
        }

        /// <summary>
        /// Gets a <see cref="Vector3"/> representation for this <see cref="Color"/>.
        /// </summary>
        /// <returns>A <see cref="Vector3"/> representation for this object.</returns>
        public Vector3 ToVector3()
        {
            return new Vector3(this.R, this.G, this.B);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (obj is Color)
            {
                Color color = (Color)obj;

                return this.backingVector == color.backingVector;
            }

            return false;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return GetHashCode(this);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            if (this.IsEmpty)
            {
                return "Color [ Empty ]";
            }

            return $"Color [ R={this.R:#0.##}, G={this.G:#0.##}, B={this.B:#0.##}, A={this.A:#0.##} ]";
        }

        /// <inheritdoc/>
        public bool Equals(Color other)
        {
            return this.backingVector.Equals(other.backingVector);
        }

        /// <summary>
        /// Gets the compressed sRGB value from an linear signal.
        /// <see href="http://www.4p8.com/eric.brasseur/gamma.html#formulas"/>
        /// <see href="http://entropymine.com/imageworsener/srgbformula/"/>
        /// </summary>
        /// <param name="signal">The signal value to compress.</param>
        /// <returns>
        /// The <see cref="float"/>.
        /// </returns>
        private static float Compand(float signal)
        {
            if (signal <= 0.0031308f)
            {
                return signal * 12.92f;
            }

            return (1.055f * (float)Math.Pow(signal, 0.41666666f)) - 0.055f;
        }

        /// <summary>
        /// Gets the expanded linear value from an sRGB signal.
        /// <see href="http://www.4p8.com/eric.brasseur/gamma.html#formulas"/>
        /// <see href="http://entropymine.com/imageworsener/srgbformula/"/>
        /// </summary>
        /// <param name="signal">The signal value to expand.</param>
        /// <returns>
        /// The <see cref="float"/>.
        /// </returns>
        private static float InverseCompand(float signal)
        {
            if (signal <= 0.04045f)
            {
                return signal / 12.92f;
            }

            return (float)Math.Pow((signal + 0.055f) / 1.055f, 2.4f);
        }

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <param name="color">
        /// The instance of <see cref="Color"/> to return the hash code for.
        /// </param>
        /// <returns>
        /// A 32-bit signed integer that is the hash code for this instance.
        /// </returns>
        private static int GetHashCode(Color color) => color.backingVector.GetHashCode();
    }
}
