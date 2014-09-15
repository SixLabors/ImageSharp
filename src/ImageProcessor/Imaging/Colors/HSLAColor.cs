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
    /// </summary>
    public struct HSLAColor
    {
        // Private data members below are on scale 0-1
        // They are scaled for use externally based on scale

        /// <summary>
        /// The hue component.
        /// </summary>
        private double hue;

        /// <summary>
        /// The luminosity component.
        /// </summary>
        private double luminosity;

        /// <summary>
        /// The saturation component.
        /// </summary>
        private double saturation;

        /// <summary>
        /// The alpha component.
        /// </summary>
        private double a;

        #region Constructors and Destructors

        /// <summary>
        /// Initializes a new instance of the <see cref="HSLAColor"/> struct.
        /// </summary>
        /// <param name="color">
        /// The color.
        /// </param>
        public HSLAColor(Color color)
            : this()
        {
            this.SetRGBA(color.R, color.G, color.B, color.A);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HSLAColor"/> class.
        /// </summary>
        /// <param name="red">
        /// The red.
        /// </param>
        /// <param name="green">
        /// The green.
        /// </param>
        /// <param name="blue">
        /// The blue.
        /// </param>
        public HSLAColor(int red, int green, int blue, int alpha)
            : this()
        {
            this.SetRGBA(red, green, blue, alpha);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HSLAColor"/> class.
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
            this.Hue = hue;
            this.Saturation = saturation;
            this.Luminosity = luminosity;
        }

        #endregion

        #region Public Properties

        /// <summary>
        /// Gets or sets the hue.
        /// </summary>
        public double Hue
        {
            get
            {
                return this.hue;
            }

            set
            {
                this.hue = this.CheckRange(value);
            }
        }

        /// <summary>
        /// Gets or sets the luminosity.
        /// </summary>
        public double Luminosity
        {
            get
            {
                return this.luminosity;
            }

            set
            {
                this.luminosity = this.CheckRange(value);
            }
        }

        /// <summary>
        /// Gets or sets the saturation.
        /// </summary>
        public double Saturation
        {
            get
            {
                return this.saturation;
            }

            set
            {
                this.saturation = this.CheckRange(value);
            }
        }

        #endregion

        #region Public Methods and Operators

        /// <summary>
        /// The op_ implicit.
        /// </summary>
        /// <param name="hslColor">
        /// The hsl color.
        /// </param>
        /// <returns>
        /// </returns>
        public static implicit operator Color(HSLAColor hslColor)
        {
            double r = 0, g = 0, b = 0;
            if (Math.Abs(hslColor.luminosity - 0) > .0001)
            {
                if (Math.Abs(hslColor.saturation - 0) <= .0001)
                {
                    r = g = b = hslColor.luminosity;
                }
                else
                {
                    double temp2 = GetTemp2(hslColor);
                    double temp1 = (2.0 * hslColor.luminosity) - temp2;

                    r = GetColorComponent(temp1, temp2, hslColor.hue + (1.0 / 3.0));
                    g = GetColorComponent(temp1, temp2, hslColor.hue);
                    b = GetColorComponent(temp1, temp2, hslColor.hue - (1.0 / 3.0));
                }
            }

            return Color.FromArgb((int)(255 * r), (int)(255 * g), (int)(255 * b));
        }

        /// <summary>
        /// The op_ implicit.
        /// </summary>
        /// <param name="color">
        /// The color.
        /// </param>
        /// <returns>
        /// </returns>
        public static implicit operator HSLAColor(Color color)
        {
            HSLAColor hslColor = new HSLAColor
            {
                hue = color.GetHue() / 360.0,
                luminosity = color.GetBrightness(),
                saturation = color.GetSaturation()
            };

            return hslColor;
        }

        /// <summary>
        /// The set rgb components.
        /// </summary>
        /// <param name="red">
        /// The red.
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
        public void SetRGBA(int red, int green, int blue, int alpha)
        {
            HSLAColor hslColor = Color.FromArgb(alpha, red, green, blue);
            this.hue = hslColor.hue;
            this.saturation = hslColor.saturation;
            this.luminosity = hslColor.luminosity;
            this.a = hslColor.a;
        }

        /// <summary>
        /// The to rgb string.
        /// </summary>
        /// <returns>
        /// The <see cref="string"/>.
        /// </returns>
        public string ToRGBString()
        {
            Color color = this;
            return string.Format("R: {0:#0.##} G: {1:#0.##} B: {2:#0.##}", color.R, color.G, color.B);
        }

        /// <summary>
        /// The to string.
        /// </summary>
        /// <returns>
        /// The <see cref="string"/>.
        /// </returns>
        public override string ToString()
        {
            return string.Format("H: {0:#0.##} S: {1:#0.##} L: {2:#0.##}", this.Hue, this.Saturation, this.Luminosity);
        }

        #endregion

        #region Methods

        /// <summary>
        /// The get color component.
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
        /// The hsl color.
        /// </param>
        /// <returns>
        /// The <see cref="double"/>.
        /// </returns>
        private static double GetTemp2(HSLAColor hslColor)
        {
            double temp2;
            if (hslColor.luminosity <= 0.5)
            {
                temp2 = hslColor.luminosity * (1.0 + hslColor.saturation);
            }
            else
            {
                temp2 = hslColor.luminosity + hslColor.saturation - (hslColor.luminosity * hslColor.saturation);
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
        /// The check range.
        /// </summary>
        /// <param name="value">
        /// The value.
        /// </param>
        /// <returns>
        /// The <see cref="double"/>.
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

        #endregion
    }
}