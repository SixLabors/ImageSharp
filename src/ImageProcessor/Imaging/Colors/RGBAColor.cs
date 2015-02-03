// --------------------------------------------------------------------------------------------------------------------
// <copyright file="RgbaColor.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging.Colors
{
    using System.Drawing;

    /// <summary>
    /// Represents an RGBA (red, green, blue, alpha) color.
    /// </summary>
    public struct RgbaColor
    {
        /// <summary>
        /// Represents a <see cref="RgbaColor"/> that is null.
        /// </summary>
        public static readonly RgbaColor Empty = new RgbaColor();

        /// <summary>
        /// The red component.
        /// </summary>
        private readonly byte r;

        /// <summary>
        /// The green component.
        /// </summary>
        private readonly byte g;

        /// <summary>
        /// The blue component.
        /// </summary>
        private readonly byte b;

        /// <summary>
        /// The alpha component.
        /// </summary>
        private readonly byte a;

        /// <summary>
        /// Initializes a new instance of the <see cref="RgbaColor"/> struct. 
        /// </summary>
        /// <param name="red">
        /// The red component.
        /// </param>
        /// <param name="green">
        /// The green component.
        /// </param>
        /// <param name="blue">
        /// The blue component.
        /// </param>
        /// <param name="alpha">
        /// The alpha component.
        /// </param>
        private RgbaColor(byte red, byte green, byte blue, byte alpha)
        {
            this.r = red;
            this.g = green;
            this.b = blue;
            this.a = alpha;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RgbaColor"/> struct. 
        /// </summary>
        /// <param name="color">
        /// The <see cref="System.Drawing.Color">color.</see>
        /// </param>
        private RgbaColor(Color color)
        {
            this.r = color.R;
            this.g = color.G;
            this.b = color.B;
            this.a = color.A;
        }

        /// <summary>
        /// Gets the red component.
        /// </summary>
        public byte R
        {
            get
            {
                return this.r;
            }
        }

        /// <summary>
        /// Gets the green component.
        /// </summary>
        public byte G
        {
            get
            {
                return this.g;
            }
        }

        /// <summary>
        /// Gets the blue component.
        /// </summary>
        public byte B
        {
            get
            {
                return this.b;
            }
        }

        /// <summary>
        /// Gets the alpha component.
        /// </summary>
        public byte A
        {
            get
            {
                return this.a;
            }
        }

        /// <summary>
        /// Creates a <see cref="RgbaColor"/> structure from the three 8-bit RGBA 
        /// components (red, green, and blue) values.
        /// </summary>
        /// <param name="red">
        /// The red component.
        /// </param>
        /// <param name="green">
        /// The green component.
        /// </param>
        /// <param name="blue">
        /// The blue component.
        /// </param>
        /// <returns>
        /// The <see cref="RgbaColor"/>.
        /// </returns>
        public static RgbaColor FromRgba(byte red, byte green, byte blue)
        {
            return new RgbaColor(red, green, blue, 255);
        }

        /// <summary>
        /// Creates a <see cref="RgbaColor"/> structure from the four 8-bit RGBA 
        /// components (red, green, blue, and alpha) values.
        /// </summary>
        /// <param name="red">
        /// The red component.
        /// </param>
        /// <param name="green">
        /// The green component.
        /// </param>
        /// <param name="blue">
        /// The blue component.
        /// </param>
        /// <param name="alpha">
        /// The alpha component.
        /// </param>
        /// <returns>
        /// The <see cref="RgbaColor"/>.
        /// </returns>
        public static RgbaColor FromRgba(byte red, byte green, byte blue, byte alpha)
        {
            return new RgbaColor(red, green, blue, alpha);
        }

        /// <summary>
        /// Creates a <see cref="RgbaColor"/> structure from the specified <see cref="System.Drawing.Color"/> structure
        /// </summary>
        /// <param name="color">
        /// The <see cref="System.Drawing.Color"/> from which to create the new <see cref="RgbaColor"/>.
        /// </param>
        /// <returns>
        /// The <see cref="RgbaColor"/>.
        /// </returns>
        public static RgbaColor FromColor(Color color)
        {
            return new RgbaColor(color);
        }

        /// <summary>
        /// Allows the implicit conversion of an instance of <see cref="System.Drawing.Color"/> to a 
        /// <see cref="RgbaColor"/>.
        /// </summary>
        /// <param name="color">
        /// The instance of <see cref="System.Drawing.Color"/> to convert.
        /// </param>
        /// <returns>
        /// An instance of <see cref="RgbaColor"/>.
        /// </returns>
        public static implicit operator RgbaColor(Color color)
        {
            return FromColor(color);
        }

        /// <summary>
        /// Allows the implicit conversion of an instance of <see cref="HslaColor"/> to a 
        /// <see cref="RgbaColor"/>.
        /// </summary>
        /// <param name="hslaColor">
        /// The instance of <see cref="HslaColor"/> to convert.
        /// </param>
        /// <returns>
        /// An instance of <see cref="RgbaColor"/>.
        /// </returns>
        public static implicit operator RgbaColor(HslaColor hslaColor)
        {
            return FromColor(hslaColor);
        }

        /// <summary>
        /// Allows the implicit conversion of an instance of <see cref="YCbCrColor"/> to a 
        /// <see cref="RgbaColor"/>.
        /// </summary>
        /// <param name="ycbcrColor">
        /// The instance of <see cref="YCbCrColor"/> to convert.
        /// </param>
        /// <returns>
        /// An instance of <see cref="RgbaColor"/>.
        /// </returns>
        public static implicit operator RgbaColor(YCbCrColor ycbcrColor)
        {
            return FromColor(ycbcrColor);
        }

        /// <summary>
        /// Allows the implicit conversion of an instance of <see cref="RgbaColor"/> to a 
        /// <see cref="System.Drawing.Color"/>.
        /// </summary>
        /// <param name="rgbaColor">
        /// The instance of <see cref="RgbaColor"/> to convert.
        /// </param>
        /// <returns>
        /// An instance of <see cref="System.Drawing.Color"/>.
        /// </returns>
        public static implicit operator Color(RgbaColor rgbaColor)
        {
            return Color.FromArgb(rgbaColor.A, rgbaColor.R, rgbaColor.G, rgbaColor.B);
        }

        /// <summary>
        /// Allows the implicit conversion of an instance of <see cref="RgbaColor"/> to a 
        /// <see cref="HslaColor"/>.
        /// </summary>
        /// <param name="rgbaColor">
        /// The instance of <see cref="RgbaColor"/> to convert.
        /// </param>
        /// <returns>
        /// An instance of <see cref="HslaColor"/>.
        /// </returns>
        public static implicit operator HslaColor(RgbaColor rgbaColor)
        {
            return HslaColor.FromColor(rgbaColor);
        }

        /// <summary>
        /// Allows the implicit conversion of an instance of <see cref="RgbaColor"/> to a 
        /// <see cref="YCbCrColor"/>.
        /// </summary>
        /// <param name="rgbaColor">
        /// The instance of <see cref="RgbaColor"/> to convert.
        /// </param>
        /// <returns>
        /// An instance of <see cref="YCbCrColor"/>.
        /// </returns>
        public static implicit operator YCbCrColor(RgbaColor rgbaColor)
        {
            return YCbCrColor.FromColor(rgbaColor);
        }

        /// <summary>
        /// Allows the implicit conversion of an instance of <see cref="RgbaColor"/> to a 
        /// <see cref="CmykColor"/>.
        /// </summary>
        /// <param name="rgbaColor">
        /// The instance of <see cref="RgbaColor"/> to convert.
        /// </param>
        /// <returns>
        /// An instance of <see cref="CmykColor"/>.
        /// </returns>
        public static implicit operator CmykColor(RgbaColor rgbaColor)
        {
            return CmykColor.FromColor(rgbaColor);
        }

        /// <summary>
        /// Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            if (this.R == 0 && this.G == 0 && this.B == 0 && this.A == 0)
            {
                return "RGBA [Empty]";
            }

            return string.Format("RGBA [R={0}, G={1}, B={2}, A={3}]", this.R, this.G, this.B, this.A);
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
            if (obj is RgbaColor)
            {
                Color thisColor = this;
                Color otherColor = (RgbaColor)obj;

                return thisColor.Equals(otherColor);
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
            Color thisColor = this;
            return thisColor.GetHashCode();
        }
    }
}