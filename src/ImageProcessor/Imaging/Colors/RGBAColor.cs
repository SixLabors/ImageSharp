// --------------------------------------------------------------------------------------------------------------------
// <copyright file="RGBAColor.cs" company="James South">
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
            return System.Drawing.Color.FromArgb(rgba.A, rgba.R, rgba.G, rgba.B);
        }
    }
}