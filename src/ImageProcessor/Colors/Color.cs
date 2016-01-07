// <copyright file="Color.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor
{
    using System;
    using System.ComponentModel;
    using System.Numerics;

    /// <summary>
    /// Represents a four-component color using red, green, blue, and alpha data. 
    /// Each component is stored in premultiplied format multiplied by the alpha component.
    /// </summary>
    /// <remarks>
    /// This struct is fully mutable. This is done (against the guidelines) for the sake of performance,
    /// as it avoids the need to create new values for modification operations.
    /// </remarks>
    public partial struct Color : IEquatable<Color>, IAlmostEquatable<Color, float>
    {
        /// <summary>
        /// Represents an empty <see cref="Color"/> that has R, G, B, and A values set to zero.
        /// </summary>
        public static readonly Color Empty = default(Color);

        /// <summary>
        /// The epsilon for comparing floating point numbers.
        /// </summary>
        private const float Epsilon = 0.001f;

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
            this.backingVector = new Vector4(r, g, b, a);
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
                float r = Convert.ToByte(hex.Substring(2, 2), 16) / 255f;
                float g = Convert.ToByte(hex.Substring(4, 2), 16) / 255f;
                float b = Convert.ToByte(hex.Substring(6, 2), 16) / 255f;
                float a = Convert.ToByte(hex.Substring(0, 2), 16) / 255f;

                this.backingVector = FromNonPremultiplied(new Color(r, g, b, a)).ToVector4();

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
                string rh = char.ToString(hex[0]);
                string gh = char.ToString(hex[1]);
                string bh = char.ToString(hex[2]);

                this.B = Convert.ToByte(bh + bh, 16) / 255f;
                this.G = Convert.ToByte(gh + gh, 16) / 255f;
                this.R = Convert.ToByte(rh + rh, 16) / 255f;
                this.A = 1;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Color"/> struct.
        /// </summary>
        /// <param name="vector">The vector.</param>
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
        public bool IsEmpty => this.Equals(Empty);

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
            return new Color(left.backingVector + right.backingVector);
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
            return new Color(left.backingVector - right.backingVector);
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
        /// Compares two <see cref="Color"/> objects for inequality.
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

            // Premultiplied.
            return from + (to - from) * amount;
            //return (from * (1 - amount)) + to;
        }

        /// <summary>
        /// Compresses a linear color signal to its sRGB equivalent.
        /// <see href="http://www.4p8.com/eric.brasseur/gamma.html#formulas"/>
        /// <see href="http://entropymine.com/imageworsener/srgbformula/"/>
        /// </summary>
        /// <param name="linear">The <see cref="Color"/> whose signal to compress.</param>
        /// <returns>The <see cref="Color"/>.</returns>
        public static Color Compress(Color linear)
        {
            // TODO: Is there a faster way to do this?
            float r = Compress(linear.R);
            float g = Compress(linear.G);
            float b = Compress(linear.B);

            return new Color(r, g, b, linear.A);
        }

        /// <summary>
        /// Expands an sRGB color signal to its linear equivalent.
        /// <see href="http://www.4p8.com/eric.brasseur/gamma.html#formulas"/>
        /// <see href="http://entropymine.com/imageworsener/srgbformula/"/>
        /// </summary>
        /// <param name="gamma">The <see cref="Color"/> whose signal to expand.</param>
        /// <returns>The <see cref="Color"/>.</returns>
        public static Color Expand(Color gamma)
        {
            // TODO: Is there a faster way to do this?
            float r = Expand(gamma.R);
            float g = Expand(gamma.G);
            float b = Expand(gamma.B);

            return new Color(r, g, b, gamma.A);
        }

        /// <summary>
        /// Converts a non-premultipled alpha <see cref="Color"/> to a <see cref="Color"/>
        /// that contains premultiplied alpha.
        /// </summary>
        /// <param name="color">The <see cref="Color"/> to convert.</param>
        /// <returns>The <see cref="Color"/>.</returns>
        public static Color FromNonPremultiplied(Color color)
        {
            float a = color.A;
            return new Color(color.R * a, color.G * a, color.B * a, a);
        }

        /// <summary>
        /// Converts a premultipled alpha <see cref="Color"/> to a <see cref="Color"/>
        /// that contains non-premultiplied alpha.
        /// </summary>
        /// <param name="color">The <see cref="Color"/> to convert.</param>
        /// <returns>The <see cref="Color"/>.</returns>
        public static Color ToNonPremultiplied(Color color)
        {
            float a = color.A;
            if (Math.Abs(a) < Epsilon)
            {
                return new Color(color.R, color.G, color.B, a);
            }

            return new Color(color.R / a, color.G / a, color.B / a, a);
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
        public override bool Equals(object obj)
        {
            if (obj is Color)
            {
                return this.Equals((Color)obj);
            }

            return false;
        }

        /// <inheritdoc/>
        public bool Equals(Color other)
        {
            return this.AlmostEquals(other, Epsilon);
        }

        /// <inheritdoc/>
        public bool AlmostEquals(Color other, float precision)
        {
            return Math.Abs(this.R - other.R) < precision
                && Math.Abs(this.G - other.G) < precision
                && Math.Abs(this.B - other.B) < precision
                && Math.Abs(this.A - other.A) < precision;
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
        private static float Compress(float signal)
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
        private static float Expand(float signal)
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
