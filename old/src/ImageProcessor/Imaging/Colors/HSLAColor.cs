// --------------------------------------------------------------------------------------------------------------------
// <copyright file="HSLAColor.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging.Colors
{
    using System;
    using System.Drawing;

    using ImageProcessor.Imaging.Helpers;

    /// <summary>
    /// Represents an HSLA (hue, saturation, luminosity, alpha) color.
    /// Adapted from <see href="http://richnewman.wordpress.com/about/code-listings-and-diagrams/hslcolor-class/"/>
    /// </summary>
    public struct HslaColor
    {
        /// <summary>
        /// Represents a <see cref="HslaColor"/> that is null.
        /// </summary>
        public static readonly HslaColor Empty = new HslaColor();

        // Private data members below are on scale 0-1
        // They are scaled for use externally based on scale

        /// <summary>
        /// The hue component.
        /// </summary>
        private readonly float h;

        /// <summary>
        /// The luminosity component.
        /// </summary>
        private readonly float l;

        /// <summary>
        /// The saturation component.
        /// </summary>
        private readonly float s;

        /// <summary>
        /// The alpha component.
        /// </summary>
        private readonly float a;

        /// <summary>
        /// Initializes a new instance of the <see cref="HslaColor"/> struct.
        /// </summary>
        /// <param name="hue">
        /// The hue component.
        /// </param>
        /// <param name="saturation">
        /// The saturation component.
        /// </param>
        /// <param name="luminosity">
        /// The luminosity component.
        /// </param>
        /// <param name="alpha">
        /// The alpha component.
        /// </param>
        private HslaColor(float hue, float saturation, float luminosity, float alpha)
        {
            this.h = Clamp(hue);
            this.s = Clamp(saturation);
            this.l = Clamp(luminosity);
            this.a = Clamp(alpha);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HslaColor"/> struct.
        /// </summary>
        /// <param name="color">
        /// The <see cref="System.Drawing.Color"/> to initialize from.
        /// </param>
        private HslaColor(Color color)
        {
            HslaColor hslColor = color;
            this.h = hslColor.h;
            this.s = hslColor.s;
            this.l = hslColor.l;
            this.a = hslColor.a;
        }

        /// <summary>
        /// Gets the hue component.
        /// <remarks>A value ranging between 0 and 1.</remarks>
        /// </summary>
        public float H
        {
            get
            {
                return this.h;
            }
        }

        /// <summary>
        /// Gets the luminosity component.
        /// <remarks>A value ranging between 0 and 1.</remarks>
        /// </summary>
        public float L
        {
            get
            {
                return this.l;
            }
        }

        /// <summary>
        /// Gets the saturation component.
        /// <remarks>A value ranging between 0 and 1.</remarks>
        /// </summary>
        public float S
        {
            get
            {
                return this.s;
            }
        }

        /// <summary>
        /// Gets the alpha component.
        /// <remarks>A value ranging between 0 and 1.</remarks>
        /// </summary>
        public float A
        {
            get
            {
                return this.a;
            }
        }

        /// <summary>
        /// Creates a <see cref="HslaColor"/> structure from the three 32-bit HSLA 
        /// components (hue, saturation, and luminosity) values.
        /// </summary>
        /// <param name="hue">
        /// The hue component.
        /// </param>
        /// <param name="saturation">
        /// The saturation component.
        /// </param>
        /// <param name="luminosity">
        /// The luminosity component.
        /// </param>
        /// <returns>
        /// The <see cref="HslaColor"/>.
        /// </returns>
        public static HslaColor FromHslaColor(float hue, float saturation, float luminosity)
        {
            return new HslaColor(hue, saturation, luminosity, 1.0f);
        }

        /// <summary>
        /// Creates a <see cref="HslaColor"/> structure from the four 32-bit HSLA 
        /// components (hue, saturation, luminosity, and alpha) values.
        /// </summary>
        /// <param name="hue">
        /// The hue component.
        /// </param>
        /// <param name="saturation">
        /// The saturation component.
        /// </param>
        /// <param name="luminosity">
        /// The luminosity component.
        /// </param>
        /// <param name="alpha">
        /// The alpha component.
        /// </param>
        /// <returns>
        /// The <see cref="HslaColor"/>.
        /// </returns>
        public static HslaColor FromHslaColor(float hue, float saturation, float luminosity, float alpha)
        {
            return new HslaColor(hue, saturation, luminosity, alpha);
        }

        /// <summary>
        /// Creates a <see cref="HslaColor"/> structure from the specified <see cref="System.Drawing.Color"/> structure
        /// </summary>
        /// <param name="color">
        /// The <see cref="System.Drawing.Color"/> from which to create the new <see cref="HslaColor"/>.
        /// </param>
        /// <returns>
        /// The <see cref="HslaColor"/>.
        /// </returns>
        public static HslaColor FromColor(Color color)
        {
            return new HslaColor(color);
        }

        /// <summary>
        /// Allows the implicit conversion of an instance of <see cref="System.Drawing.Color"/> to a 
        /// <see cref="HslaColor"/>.
        /// </summary>
        /// <param name="color">
        /// The instance of <see cref="System.Drawing.Color"/> to convert.
        /// </param>
        /// <returns>
        /// An instance of <see cref="HslaColor"/>.
        /// </returns>
        public static implicit operator HslaColor(Color color)
        {
            HslaColor hslColor = new HslaColor(
                  color.GetHue() / 360.0f,
                  color.GetSaturation(),
                  color.GetBrightness(),
                  color.A / 255f);

            return hslColor;
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
            return FromColor(rgbaColor);
        }

        /// <summary>
        /// Allows the implicit conversion of an instance of <see cref="YCbCrColor"/> to a 
        /// <see cref="HslaColor"/>.
        /// </summary>
        /// <param name="ycbcrColor">
        /// The instance of <see cref="YCbCrColor"/> to convert.
        /// </param>
        /// <returns>
        /// An instance of <see cref="YCbCrColor"/>.
        /// </returns>
        public static implicit operator HslaColor(YCbCrColor ycbcrColor)
        {
            Color color = ycbcrColor;
            HslaColor hslColor = new HslaColor(
                color.GetHue() / 360.0f,
                color.GetSaturation(),
                color.GetBrightness(),
                color.A / 255f);

            return hslColor;
        }

        /// <summary>
        /// Allows the implicit conversion of an instance of <see cref="HslaColor"/> to a 
        /// <see cref="System.Drawing.Color"/>.
        /// </summary>
        /// <param name="hslaColor">
        /// The instance of <see cref="HslaColor"/> to convert.
        /// </param>
        /// <returns>
        /// An instance of <see cref="System.Drawing.Color"/>.
        /// </returns>
        public static implicit operator Color(HslaColor hslaColor)
        {
            float r = 0, g = 0, b = 0;
            if (Math.Abs(hslaColor.l - 0) > .0001)
            {
                if (Math.Abs(hslaColor.s - 0) <= .0001)
                {
                    r = g = b = hslaColor.l;
                }
                else
                {
                    float temp2 = GetTemp2(hslaColor);
                    float temp1 = (2.0f * hslaColor.l) - temp2;

                    r = GetColorComponent(temp1, temp2, hslaColor.h + (1.0f / 3.0f));
                    g = GetColorComponent(temp1, temp2, hslaColor.h);
                    b = GetColorComponent(temp1, temp2, hslaColor.h - (1.0f / 3.0f));
                }
            }

            return Color.FromArgb(
                Convert.ToInt32(255 * hslaColor.a),
                Convert.ToInt32(255 * r),
                Convert.ToInt32(255 * g),
                Convert.ToInt32(255 * b));
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
            float r = 0, g = 0, b = 0;
            if (Math.Abs(hslaColor.l - 0) > .0001)
            {
                if (Math.Abs(hslaColor.s - 0) <= .0001)
                {
                    r = g = b = hslaColor.l;
                }
                else
                {
                    float temp2 = GetTemp2(hslaColor);
                    float temp1 = (2.0f * hslaColor.l) - temp2;

                    r = GetColorComponent(temp1, temp2, hslaColor.h + (1.0f / 3.0f));
                    g = GetColorComponent(temp1, temp2, hslaColor.h);
                    b = GetColorComponent(temp1, temp2, hslaColor.h - (1.0f / 3.0f));
                }
            }

            return RgbaColor.FromRgba(
                Convert.ToByte(255 * r),
                Convert.ToByte(255 * g),
                Convert.ToByte(255 * b),
                Convert.ToByte(255 * hslaColor.a));
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
            return YCbCrColor.FromColor(hslaColor);
        }

        /// <summary>
        /// Allows the implicit conversion of an instance of <see cref="HslaColor"/> to a 
        /// <see cref="CmykColor"/>.
        /// </summary>
        /// <param name="hslaColor">
        /// The instance of <see cref="HslaColor"/> to convert.
        /// </param>
        /// <returns>
        /// An instance of <see cref="CmykColor"/>.
        /// </returns>
        public static implicit operator CmykColor(HslaColor hslaColor)
        {
            return CmykColor.FromColor(hslaColor);
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
                return "HslaColor [Empty]";
            }

            return string.Format("HslaColor [ H={0:#0.##}, S={1:#0.##}, L={2:#0.##}, A={3:#0.##}]", this.H, this.S, this.L, this.A);
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
            if (obj is HslaColor)
            {
                Color thisColor = this;
                Color otherColor = (HslaColor)obj;

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
        /// Gets the color component from the given hue values.
        /// </summary>
        /// <param name="temp1">
        /// The temp 1.
        /// </param>
        /// <param name="temp2">
        /// The temp 2.
        /// </param>
        /// <param name="temp3">
        /// The temp 3.
        /// </param>
        /// <returns>
        /// The <see cref="float"/>.
        /// </returns>
        private static float GetColorComponent(float temp1, float temp2, float temp3)
        {
            temp3 = MoveIntoRange(temp3);
            if (temp3 < 1.0 / 6.0)
            {
                return temp1 + ((temp2 - temp1) * 6.0f * temp3);
            }

            if (temp3 < 0.5)
            {
                return temp2;
            }

            if (temp3 < 2.0 / 3.0)
            {
                return temp1 + ((temp2 - temp1) * ((2.0f / 3.0f) - temp3) * 6.0f);
            }

            return temp1;
        }

        /// <summary>
        /// The get temp 2.
        /// </summary>
        /// <param name="hslColor">
        /// The <see cref="HslaColor"/> color.
        /// </param>
        /// <returns>
        /// The <see cref="float"/>.
        /// </returns>
        private static float GetTemp2(HslaColor hslColor)
        {
            float temp2;
            if (hslColor.l <= 0.5)
            {
                temp2 = hslColor.l * (1.0f + hslColor.s);
            }
            else
            {
                temp2 = hslColor.l + hslColor.s - (hslColor.l * hslColor.s);
            }

            return temp2;
        }

        /// <summary>
        /// The move into range.
        /// </summary>
        /// <param name="temp3">
        /// The temp 3.
        /// </param>
        /// <returns>
        /// The <see cref="float"/>.
        /// </returns>
        private static float MoveIntoRange(float temp3)
        {
            if (temp3 < 0.0)
            {
                temp3 += 1.0f;
            }
            else if (temp3 > 1.0)
            {
                temp3 -= 1.0f;
            }

            return temp3;
        }

        /// <summary>
        /// Checks the range of the given value to ensure that it remains within the acceptable boundaries.
        /// </summary>
        /// <param name="value">
        /// The value to check.
        /// </param>
        /// <returns>
        /// The sanitized <see cref="float"/>.
        /// </returns>
        private static float Clamp(float value)
        {
            return ImageMaths.Clamp(value, 0, 1);
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
            return Math.Abs(this.h - 0) <= Epsilon && Math.Abs(this.s - 0) <= Epsilon &&
                   Math.Abs(this.l - 0) <= Epsilon && Math.Abs(this.a - 0) <= Epsilon;
        }
    }
}