// <copyright file="Color.cs" company="James South">
// Copyright (c) James South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor
{
    using System.Numerics;

    /// <summary>
    /// Represents a four-component color using red, green, blue, and alpha data.
    /// </summary>
    /// <remarks>
    /// This struct is fully mutable. This is done (against the guidelines) for the sake of performance,
    /// as it avoids the need to create new values for modification operations.
    /// </remarks>
    public struct Color
    {
        /// <summary>
        /// The backing vector for SIMD support.
        /// </summary>
        private Vector4 backingVector;

        /// <summary>
        /// Initializes a new instance of the <see cref="Color"/> struct.
        /// </summary>
        /// <param name="r">
        /// The red component of this <see cref="Color"/>.
        /// </param>
        /// <param name="g">
        /// The green component of this <see cref="Color"/>.
        /// </param>
        /// <param name="b">
        /// The blue component of this <see cref="Color"/>.
        /// </param>
        /// <param name="a">
        /// The alpha component of this <see cref="Color"/>.
        /// </param>
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
        /// <param name="vector">
        /// The vector.
        /// </param>
        private Color(Vector4 vector)
        {
            this.backingVector = vector;
        }

        /// <summary>
        /// Gets or sets the blue component of the color.
        /// </summary>
        public float B
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
        /// Gets or sets the red component of the color.
        /// </summary>
        public float R
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
        /// <param name="color">
        /// The instance of <see cref="Color"/> to convert.
        /// </param>
        /// <returns>
        /// An instance of <see cref="Bgra32"/>.
        /// </returns>
        public static implicit operator Color(Bgra32 color)
        {
            return new Color(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);
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
    }
}
