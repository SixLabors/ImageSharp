// --------------------------------------------------------------------------------------------------------------------
// <copyright file="RGBAColor.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging.Colors
{
    using System.Drawing;
    using System.Text;

    /// <summary>
    /// Represents an RGBA (red, green, blue, alpha) color.
    /// </summary>
    public struct RGBAColor
    {
        /// <summary>
        /// The alpha component.
        /// </summary>
        public byte A;

        /// <summary>
        /// The blue component.
        /// </summary>
        public byte B;

        /// <summary>
        /// The green component.
        /// </summary>
        public byte G;

        /// <summary>
        /// The red component.
        /// </summary>
        public byte R;

        /// <summary>
        /// Initializes a new instance of the <see cref="RGBAColor"/> struct.
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
        public RGBAColor(byte red, byte green, byte blue)
        {
            this.R = red;
            this.G = green;
            this.B = blue;
            this.A = 255;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RGBAColor"/> struct. 
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
        public RGBAColor(byte red, byte green, byte blue, byte alpha)
        {
            this.R = red;
            this.G = green;
            this.B = blue;
            this.A = alpha;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RGBAColor"/> struct. 
        /// </summary>
        /// <param name="color">
        /// The <see cref="System.Drawing.Color">color.</see>
        /// </param>
        public RGBAColor(Color color)
        {
            this.R = color.R;
            this.G = color.G;
            this.B = color.B;
            this.A = color.A;
        }

        /// <summary>
        /// Allows the implicit conversion of an instance of <see cref="System.Drawing.Color"/> to a 
        /// <see cref="RGBAColor"/>.
        /// </summary>
        /// <param name="color">
        /// The instance of <see cref="System.Drawing.Color"/> to convert.
        /// </param>
        /// <returns>
        /// An instance of <see cref="RGBAColor"/>.
        /// </returns>
        public static implicit operator RGBAColor(Color color)
        {
            return new RGBAColor(color);
        }

        /// <summary>
        /// Allows the implicit conversion of an instance of <see cref="System.Drawing.Color"/> to a 
        /// <see cref="RGBAColor"/>.
        /// </summary>
        /// <param name="color">
        /// The instance of <see cref="System.Drawing.Color"/> to convert.
        /// </param>
        /// <returns>
        /// An instance of <see cref="RGBAColor"/>.
        /// </returns>
        public static implicit operator RGBAColor(HSLAColor color)
        {
            return new RGBAColor(color);
        }

        /// <summary>
        /// Allows the implicit conversion of an instance of <see cref="RGBAColor"/> to a 
        /// <see cref="System.Drawing.Color"/>.
        /// </summary>
        /// <param name="rgba">
        /// The instance of <see cref="RGBAColor"/> to convert.
        /// </param>
        /// <returns>
        /// An instance of <see cref="System.Drawing.Color"/>.
        /// </returns>
        public static implicit operator Color(RGBAColor rgba)
        {
            return Color.FromArgb(rgba.A, rgba.R, rgba.G, rgba.B);
        }

        /// <summary>
        /// Allows the implicit conversion of an instance of <see cref="RGBAColor"/> to a 
        /// <see cref="HSLAColor"/>.
        /// </summary>
        /// <param name="rgba">
        /// The instance of <see cref="RGBAColor"/> to convert.
        /// </param>
        /// <returns>
        /// An instance of <see cref="HSLAColor"/>.
        /// </returns>
        public static implicit operator HSLAColor(RGBAColor rgba)
        {
            return new HSLAColor(rgba);
        }

        /// <summary>
        /// Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return string.Format("R={0}, G={1}, B={2}, A={3}", this.R, this.G, this.B, this.A);
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
            if (obj is RGBAColor)
            {
                Color thisColor = this;
                Color otherColor = (RGBAColor)obj;

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