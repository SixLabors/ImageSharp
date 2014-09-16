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

    /// <summary>
    /// Represents an HSLA (hue, saturation, luminosity, alpha) color.
    /// Adapted from <see href="http://richnewman.wordpress.com/about/code-listings-and-diagrams/hslcolor-class/"/>
    /// </summary>
    public struct HSLAColor
    {
        // Private data members below are on scale 0-1
        // They are scaled for use externally based on scale

        /// <summary>
        /// The hue component.
        /// </summary>
        private double h;

        /// <summary>
        /// The luminosity component.
        /// </summary>
        private double l;

        /// <summary>
        /// The saturation component.
        /// </summary>
        private double s;

        /// <summary>
        /// The alpha component.
        /// </summary>
        private double a;

        /// <summary>
        /// Initializes a new instance of the <see cref="HSLAColor"/> struct.
        /// </summary>
        /// <param name="hue">
        /// The hue.
        /// </param>
        /// <param name="saturation">
        /// The saturation.
        /// </param>
        /// <param name="luminosity">
        /// The luminosity.
        /// </param>
        public HSLAColor(double hue, double saturation, double luminosity)
            : this()
        {
            this.H = hue;
            this.S = saturation;
            this.L = luminosity;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HSLAColor"/> struct.
        /// </summary>
        /// <param name="color">
        /// The <see cref="System.Drawing.Color"/> to initialize from.
        /// </param>
        public HSLAColor(Color color)
            : this()
        {
            HSLAColor hslColor = color;
            this.h = hslColor.h;
            this.s = hslColor.s;
            this.l = hslColor.l;
            this.a = hslColor.a;
        }

        /// <summary>
        /// Gets or sets the hue component.
        /// </summary>
        public double H
        {
            get
            {
                return this.h;
            }

            set
            {
                this.h = this.CheckRange(value);
            }
        }

        /// <summary>
        /// Gets or sets the luminosity component.
        /// </summary>
        public double L
        {
            get
            {
                return this.l;
            }

            set
            {
                this.l = this.CheckRange(value);
            }
        }

        /// <summary>
        /// Gets or sets the saturation component.
        /// </summary>
        public double S
        {
            get
            {
                return this.s;
            }

            set
            {
                this.s = this.CheckRange(value);
            }
        }

        /// <summary>
        /// Gets or sets the alpha component.
        /// </summary>
        public double A
        {
            get
            {
                return this.a;
            }

            set
            {
                this.a = this.CheckRange(value);
            }
        }

        /// <summary>
        /// Allows the implicit conversion of an instance of <see cref="System.Drawing.Color"/> to a 
        /// <see cref="HSLAColor"/>.
        /// </summary>
        /// <param name="color">
        /// The instance of <see cref="System.Drawing.Color"/> to convert.
        /// </param>
        /// <returns>
        /// An instance of <see cref="HSLAColor"/>.
        /// </returns>
        public static implicit operator HSLAColor(Color color)
        {
            HSLAColor hslColor = new HSLAColor
            {
                h = color.GetHue() / 360.0,
                l = color.GetBrightness(),
                s = color.GetSaturation(),
                a = color.A / 255f
            };

            return hslColor;
        }

        /// <summary>
        /// Allows the implicit conversion of an instance of <see cref="System.Drawing.Color"/> to a 
        /// <see cref="HSLAColor"/>.
        /// </summary>
        /// <param name="rgbaColor">
        /// The instance of <see cref="RGBAColor"/> to convert.
        /// </param>
        /// <returns>
        /// An instance of <see cref="HSLAColor"/>.
        /// </returns>
        public static implicit operator HSLAColor(RGBAColor rgbaColor)
        {
            Color color = rgbaColor;
            HSLAColor hslColor = new HSLAColor
            {
                h = color.GetHue() / 360.0,
                l = color.GetBrightness(),
                s = color.GetSaturation(),
                a = color.A / 255f
            };

            return hslColor;
        }

        /// <summary>
        /// Allows the implicit conversion of an instance of <see cref="HSLAColor"/> to a 
        /// <see cref="System.Drawing.Color"/>.
        /// </summary>
        /// <param name="hslaColor">
        /// The instance of <see cref="HSLAColor"/> to convert.
        /// </param>
        /// <returns>
        /// An instance of <see cref="System.Drawing.Color"/>.
        /// </returns>
        public static implicit operator Color(HSLAColor hslaColor)
        {
            double r = 0, g = 0, b = 0;
            if (Math.Abs(hslaColor.l - 0) > .0001)
            {
                if (Math.Abs(hslaColor.s - 0) <= .0001)
                {
                    r = g = b = hslaColor.l;
                }
                else
                {
                    double temp2 = GetTemp2(hslaColor);
                    double temp1 = (2.0 * hslaColor.l) - temp2;

                    r = GetColorComponent(temp1, temp2, hslaColor.h + (1.0 / 3.0));
                    g = GetColorComponent(temp1, temp2, hslaColor.h);
                    b = GetColorComponent(temp1, temp2, hslaColor.h - (1.0 / 3.0));
                }
            }

            return Color.FromArgb(Convert.ToInt32(255 * hslaColor.a), Convert.ToInt32(255 * r), Convert.ToInt32(255 * g), Convert.ToInt32(255 * b));
        }

        /// <summary>
        /// Allows the implicit conversion of an instance of <see cref="HSLAColor"/> to a 
        /// <see cref="RGBAColor"/>.
        /// </summary>
        /// <param name="hslaColor">
        /// The instance of <see cref="HSLAColor"/> to convert.
        /// </param>
        /// <returns>
        /// An instance of <see cref="RGBAColor"/>.
        /// </returns>
        public static implicit operator RGBAColor(HSLAColor hslaColor)
        {
            double r = 0, g = 0, b = 0;
            if (Math.Abs(hslaColor.l - 0) > .0001)
            {
                if (Math.Abs(hslaColor.s - 0) <= .0001)
                {
                    r = g = b = hslaColor.l;
                }
                else
                {
                    double temp2 = GetTemp2(hslaColor);
                    double temp1 = (2.0 * hslaColor.l) - temp2;

                    r = GetColorComponent(temp1, temp2, hslaColor.h + (1.0 / 3.0));
                    g = GetColorComponent(temp1, temp2, hslaColor.h);
                    b = GetColorComponent(temp1, temp2, hslaColor.h - (1.0 / 3.0));
                }
            }

            return new RGBAColor(Convert.ToByte(255 * r), Convert.ToByte(255 * g), Convert.ToByte(255 * b), Convert.ToByte(255 * hslaColor.a));
        }

        /// <summary>
        /// Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String" /> that represents this instance.
        /// </returns>
        public string ToRGBAString()
        {
            Color color = this;
            return string.Format("R={0}, G={1}, B={2}, A={3}", color.R, color.G, color.B, color.A);
        }

        /// <summary>
        /// Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return string.Format("H={0:#0.##}, S={1:#0.##}, L={2:#0.##}, A={3:#0.##}", this.H, this.S, this.L, this.A);
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
            if (obj is HSLAColor)
            {
                Color thisColor = this;
                Color otherColor = (HSLAColor)obj;

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
        /// The <see cref="double"/>.
        /// </returns>
        private static double GetColorComponent(double temp1, double temp2, double temp3)
        {
            temp3 = MoveIntoRange(temp3);
            if (temp3 < 1.0 / 6.0)
            {
                return temp1 + ((temp2 - temp1) * 6.0 * temp3);
            }

            if (temp3 < 0.5)
            {
                return temp2;
            }

            if (temp3 < 2.0 / 3.0)
            {
                return temp1 + ((temp2 - temp1) * ((2.0 / 3.0) - temp3) * 6.0);
            }

            return temp1;
        }

        /// <summary>
        /// The get temp 2.
        /// </summary>
        /// <param name="hslColor">
        /// The <see cref="HSLAColor"/> color.
        /// </param>
        /// <returns>
        /// The <see cref="double"/>.
        /// </returns>
        private static double GetTemp2(HSLAColor hslColor)
        {
            double temp2;
            if (hslColor.l <= 0.5)
            {
                temp2 = hslColor.l * (1.0 + hslColor.s);
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
        /// The <see cref="double"/>.
        /// </returns>
        private static double MoveIntoRange(double temp3)
        {
            if (temp3 < 0.0)
            {
                temp3 += 1.0;
            }
            else if (temp3 > 1.0)
            {
                temp3 -= 1.0;
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
        /// The sanitized <see cref="double"/>.
        /// </returns>
        private double CheckRange(double value)
        {
            if (value < 0.0)
            {
                value = 0.0;
            }
            else if (value > 1.0)
            {
                value = 1.0;
            }

            return value;
        }
    }
}