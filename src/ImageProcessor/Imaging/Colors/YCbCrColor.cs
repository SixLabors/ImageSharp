// --------------------------------------------------------------------------------------------------------------------
// <copyright file="YCbCrColor.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Represents an YCbCr (luminance, chroma, chroma) color used in digital imaging systems.
//   <see href="http://en.wikipedia.org/wiki/YCbCr" />
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging.Colors
{
    using System;
    using System.Drawing;

    using ImageProcessor.Common.Extensions;

    /// <summary>
    /// Represents an YCbCr (luminance, chroma, chroma) color used in digital imaging systems.
    /// <see href="http://en.wikipedia.org/wiki/YCbCr"/>
    /// </summary>
    public struct YCbCrColor
    {
        /// <summary>
        /// Represents a <see cref="YCbCrColor"/> that is null.
        /// </summary>
        public static readonly YCbCrColor Empty = new YCbCrColor();

        /// <summary>
        /// The y luminance component.
        /// </summary>
        private readonly float y;

        /// <summary>
        /// The u chroma component.
        /// </summary>
        private readonly float cb;

        /// <summary>
        /// The v chroma component.
        /// </summary>
        private readonly float cr;

        /// <summary>
        /// Initializes a new instance of the <see cref="YCbCrColor"/> struct.
        /// </summary>
        /// <param name="y">The y luminance component.</param>
        /// <param name="cb">The u chroma component.</param>
        /// <param name="cr">The v chroma component.</param> 
        private YCbCrColor(float y, float cb, float cr)
        {
            this.y = y; //Math.Max(0.0f, Math.Min(1.0f, y));
            this.cb = cb; //Math.Max(-0.5f, Math.Min(0.5f, cb));
            this.cr = cr; //Math.Max(-0.5f, Math.Min(0.5f, cr));
        }

        /// <summary>
        /// Gets the Y luminance component.
        /// </summary>
        public float Y
        {
            get
            {
                return this.y;
            }
        }

        /// <summary>
        /// Gets the U chroma component.
        /// </summary>
        public float Cb
        {
            get
            {
                return this.y;
            }
        }

        /// <summary>
        /// Gets the V chroma component.
        /// </summary>
        public float Cr
        {
            get
            {
                return this.y;
            }
        }

        /// <summary>
        /// Creates a <see cref="YCbCrColor"/> structure from the three 32-bit YCbCr 
        /// components (luminance, chroma, and chroma) values.
        /// </summary>
        /// <param name="y">The y luminance component.</param>
        /// <param name="cb">The u chroma component.</param>
        /// <param name="cr">The v chroma component.</param> 
        /// <returns>
        /// The <see cref="YCbCrColor"/>.
        /// </returns>
        public static YCbCrColor FromYCbCr(float y, float cb, float cr)
        {
            return new YCbCrColor(y, cb, cr);
        }

        /// <summary>
        /// Creates a <see cref="YCbCrColor"/> structure from the specified <see cref="System.Drawing.Color"/> structure
        /// </summary>
        /// <param name="color">
        /// The <see cref="System.Drawing.Color"/> from which to create the new <see cref="YCbCrColor"/>.
        /// </param>
        /// <returns>
        /// The <see cref="YCbCrColor"/>.
        /// </returns>
        public static YCbCrColor FromColor(Color color)
        {
            float r = color.R;
            float g = color.G;
            float b = color.B;

            float y = (float)((0.299 * r) + (0.587 * g) + (0.114 * b));
            float cb = 128 - (float)((-0.168736 * r) - (0.331264 * g) + (0.5 * b));
            float cr = 128 + (float)((0.5 * r) - (0.418688 * g) - (0.081312 * b));

            return new YCbCrColor(y, cb, cr);
        }

        /// <summary>
        /// Allows the implicit conversion of an instance of <see cref="System.Drawing.Color"/> to a 
        /// <see cref="YCbCrColor"/>.
        /// </summary>
        /// <param name="color">
        /// The instance of <see cref="System.Drawing.Color"/> to convert.
        /// </param>
        /// <returns>
        /// An instance of <see cref="YCbCrColor"/>.
        /// </returns>
        public static implicit operator YCbCrColor(Color color)
        {
            return FromColor(color);
        }

        /// <summary>
        /// Allows the implicit conversion of an instance of <see cref="YCbCrColor"/> to a 
        /// <see cref="System.Drawing.Color"/>.
        /// </summary>
        /// <param name="ycbcr">
        /// The instance of <see cref="YCbCrColor"/> to convert.
        /// </param>
        /// <returns>
        /// An instance of <see cref="System.Drawing.Color"/>.
        /// </returns>
        public static implicit operator Color(YCbCrColor ycbcr)
        {
            byte r = Convert.ToInt32(ycbcr.Y + (1.402 * (ycbcr.Cr - 128))).ToByte();
            byte g = Convert.ToInt32(ycbcr.Y - (0.34414 * (ycbcr.Cb - 128)) - (0.71414 * (ycbcr.Cr - 128))).ToByte();
            byte b = Convert.ToInt32(ycbcr.Y + (1.772 * (ycbcr.Cb - 128))).ToByte();
            

            return Color.FromArgb(255, r, g, b);
        }
    }
}
