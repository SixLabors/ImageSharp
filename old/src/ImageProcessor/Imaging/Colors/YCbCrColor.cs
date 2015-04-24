// --------------------------------------------------------------------------------------------------------------------
// <copyright file="YCbCrColor.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Represents an YCbCr (luminance, chroma, chroma) color conforming to the ITU-R BT.601 standard used in digital imaging systems.
//   <see href="http://en.wikipedia.org/wiki/YCbCr" />
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging.Colors
{
    using System;
    using System.Drawing;

    using ImageProcessor.Imaging.Helpers;

    /// <summary>
    /// Represents an YCbCr (luminance, chroma, chroma) color conforming to the ITU-R BT.601 standard used in digital imaging systems.
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
            this.y = ImageMaths.Clamp(y, 0, 255);
            this.cb = ImageMaths.Clamp(cb, 0, 255);
            this.cr = ImageMaths.Clamp(cr, 0, 255);
        }

        /// <summary>
        /// Gets the Y luminance component.
        /// <remarks>A value ranging between 0 and 255.</remarks>
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
        /// <remarks>A value ranging between 0 and 255.</remarks>
        /// </summary>
        public float Cb
        {
            get
            {
                return this.cb;
            }
        }

        /// <summary>
        /// Gets the V chroma component.
        /// <remarks>A value ranging between 0 and 255.</remarks>
        /// </summary>
        public float Cr
        {
            get
            {
                return this.cr;
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
            byte r = color.R;
            byte g = color.G;
            byte b = color.B;

            float y = (float)((0.299 * r) + (0.587 * g) + (0.114 * b));
            float cb = 128 + (float)((-0.168736 * r) - (0.331264 * g) + (0.5 * b));
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
            return FromColor(rgbaColor);
        }

        /// <summary>
        /// Allows the implicit conversion of an instance of <see cref="HslaColor"/> to a 
        /// <see cref="YCbCrColor"/>.
        /// </summary>
        /// <param name="hslaColor">
        /// The instance of <see cref="HslaColor"/> to convert.
        /// </param>
        /// <returns>
        /// An instance of <see cref="YCbCrColor"/>.
        /// </returns>
        public static implicit operator YCbCrColor(HslaColor hslaColor)
        {
            return FromColor(hslaColor);
        }

        /// <summary>
        /// Allows the implicit conversion of an instance of <see cref="YCbCrColor"/> to a 
        /// <see cref="System.Drawing.Color"/>.
        /// </summary>
        /// <param name="ycbcrColor">
        /// The instance of <see cref="YCbCrColor"/> to convert.
        /// </param>
        /// <returns>
        /// An instance of <see cref="System.Drawing.Color"/>.
        /// </returns>
        public static implicit operator Color(YCbCrColor ycbcrColor)
        {
            float y = ycbcrColor.Y;
            float cb = ycbcrColor.Cb - 128;
            float cr = ycbcrColor.Cr - 128;

            //byte r = Convert.ToByte(Math.Max(0.0f, Math.Min(255f, y + (1.402 * cr))));
            //byte g = Convert.ToByte(Math.Max(0.0f, Math.Min(255f, y - (0.34414 * cb) - (0.71414 * cr))));
            //byte b = Convert.ToByte(Math.Max(0.0f, Math.Min(255f, y + (1.772 * cb))));

            byte r = Convert.ToByte(ImageMaths.Clamp(y + (1.402 * cr), 0, 255));
            byte g = Convert.ToByte(ImageMaths.Clamp(y - (0.34414 * cb) - (0.71414 * cr), 0, 255));
            byte b = Convert.ToByte(ImageMaths.Clamp(y + (1.772 * cb), 0, 255));

            return Color.FromArgb(255, r, g, b);
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
            return RgbaColor.FromColor(ycbcrColor);
        }

        /// <summary>
        /// Allows the implicit conversion of an instance of <see cref="YCbCrColor"/> to a 
        /// <see cref="HslaColor"/>.
        /// </summary>
        /// <param name="ycbcrColor">
        /// The instance of <see cref="YCbCrColor"/> to convert.
        /// </param>
        /// <returns>
        /// An instance of <see cref="HslaColor"/>.
        /// </returns>
        public static implicit operator HslaColor(YCbCrColor ycbcrColor)
        {
            return HslaColor.FromColor(ycbcrColor);
        }

        /// <summary>
        /// Allows the implicit conversion of an instance of <see cref="YCbCrColor"/> to a 
        /// <see cref="CmykColor"/>.
        /// </summary>
        /// <param name="ycbcrColor">
        /// The instance of <see cref="YCbCrColor"/> to convert.
        /// </param>
        /// <returns>
        /// An instance of <see cref="CmykColor"/>.
        /// </returns>
        public static implicit operator CmykColor(YCbCrColor ycbcrColor)
        {
            return CmykColor.FromColor(ycbcrColor);
        }

        /// <summary>
        /// Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            if (this.IsEmpty())
            {
                return "YCbCrColor [Empty]";
            }

            return string.Format("YCbCrColor [ Y={0:#0.##}, Cb={1:#0.##}, Cr={2:#0.##}]", this.Y, this.Cb, this.Cr);
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
            if (obj is YCbCrColor)
            {
                Color thisColor = this;
                Color otherColor = (YCbCrColor)obj;

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

        /// <summary>
        /// Returns a value indicating whether the current instance is empty.
        /// </summary>
        /// <returns>
        /// The true if this instance is empty; otherwise, false.
        /// </returns>
        private bool IsEmpty()
        {
            const float Epsilon = .0001f;
            return Math.Abs(this.y - 0) <= Epsilon && Math.Abs(this.cb - 0) <= Epsilon &&
                   Math.Abs(this.cr - 0) <= Epsilon;
        }
    }
}
