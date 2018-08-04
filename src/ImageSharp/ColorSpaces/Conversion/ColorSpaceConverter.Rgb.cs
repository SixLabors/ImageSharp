// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation.RgbColorSapce;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion
{
    /// <content>
    /// Allows conversion to <see cref="Rgb"/>.
    /// </content>
    internal partial class ColorSpaceConverter
    {
        private static readonly LinearRgbToRgbConverter LinearRgbToRgbConverter = new LinearRgbToRgbConverter();

        /// <summary>
        /// Converts a <see cref="CieLab"/> into a <see cref="Rgb"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Rgb"/></returns>
        public Rgb ToRgb(in CieLab color)
        {
            var xyzColor = this.ToCieXyz(color);
            return this.ToRgb(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieLch"/> into a <see cref="Rgb"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Rgb"/></returns>
        public Rgb ToRgb(in CieLch color)
        {
            var xyzColor = this.ToCieXyz(color);
            return this.ToRgb(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieLchuv"/> into a <see cref="Rgb"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Rgb"/></returns>
        public Rgb ToRgb(in CieLchuv color)
        {
            var xyzColor = this.ToCieXyz(color);
            return this.ToRgb(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieLuv"/> into a <see cref="Rgb"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Rgb"/></returns>
        public Rgb ToRgb(in CieLuv color)
        {
            var xyzColor = this.ToCieXyz(color);
            return this.ToRgb(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieXyy"/> into a <see cref="Rgb"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Rgb"/></returns>
        public Rgb ToRgb(in CieXyy color)
        {
            var xyzColor = this.ToCieXyz(color);
            return this.ToRgb(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieXyz"/> into a <see cref="Rgb"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Rgb"/></returns>
        public Rgb ToRgb(in CieXyz color)
        {
            // Conversion
            var linear = this.ToLinearRgb(color);

            // Compand
            return this.ToRgb(linear);
        }

        /// <summary>
        /// Converts a <see cref="Cmyk"/> into a <see cref="Rgb"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Rgb"/></returns>
        public Rgb ToRgb(in Cmyk color)
        {
            // Conversion
            return CmykAndRgbConverter.Convert(color);
        }

        /// <summary>
        /// Converts a <see cref="Hsv"/> into a <see cref="Rgb"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Rgb"/></returns>
        public Rgb ToRgb(in Hsv color)
        {
            // Conversion
            return HsvAndRgbConverter.Convert(color);
        }

        /// <summary>
        /// Converts a <see cref="Hsl"/> into a <see cref="Rgb"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Rgb"/></returns>
        public Rgb ToRgb(in Hsl color)
        {
            // Conversion
            return HslAndRgbConverter.Convert(color);
        }

        /// <summary>
        /// Converts a <see cref="HunterLab"/> into a <see cref="Rgb"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Rgb"/></returns>
        public Rgb ToRgb(in HunterLab color)
        {
            var xyzColor = this.ToCieXyz(color);
            return this.ToRgb(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="LinearRgb"/> into a <see cref="Rgb"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Rgb"/></returns>
        public Rgb ToRgb(in LinearRgb color)
        {
            // Conversion
            return LinearRgbToRgbConverter.Convert(color);
        }

        /// <summary>
        /// Converts a <see cref="Lms"/> into a <see cref="Rgb"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Rgb"/></returns>
        public Rgb ToRgb(in Lms color)
        {
            var xyzColor = this.ToCieXyz(color);
            return this.ToRgb(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="YCbCr"/> into a <see cref="Rgb"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Rgb"/></returns>
        public Rgb ToRgb(in YCbCr color)
        {
            // Conversion
            Rgb rgb = YCbCrAndRgbConverter.Convert(color);

            // Adaptation
            return this.Adapt(rgb);
        }
    }
}