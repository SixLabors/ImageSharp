// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation.YCbCrColorSapce;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion
{
    /// <content>
    /// Allows conversion to <see cref="YCbCr"/>.
    /// </content>
    internal partial class ColorSpaceConverter
    {
        private static readonly YCbCrAndRgbConverter YCbCrAndRgbConverter = new YCbCrAndRgbConverter();

        /// <summary>
        /// Converts a <see cref="CieLab"/> into a <see cref="YCbCr"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="YCbCr"/></returns>
        public YCbCr ToYCbCr(in CieLab color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToYCbCr(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieLch"/> into a <see cref="YCbCr"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="YCbCr"/></returns>
        public YCbCr ToYCbCr(in CieLch color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToYCbCr(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieLchuv"/> into a <see cref="YCbCr"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="YCbCr"/></returns>
        public YCbCr ToYCbCr(in CieLchuv color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToYCbCr(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieLuv"/> into a <see cref="YCbCr"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="YCbCr"/></returns>
        public YCbCr ToYCbCr(in CieLuv color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToYCbCr(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieXyy"/> into a <see cref="YCbCr"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="YCbCr"/></returns>
        public YCbCr ToYCbCr(in CieXyy color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToYCbCr(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieXyz"/> into a <see cref="YCbCr"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="YCbCr"/></returns>
        public YCbCr ToYCbCr(in CieXyz color)
        {
            var rgb = this.ToRgb(color);

            return YCbCrAndRgbConverter.Convert(rgb);
        }

        /// <summary>
        /// Converts a <see cref="Cmyk"/> into a <see cref="YCbCr"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="YCbCr"/></returns>
        public YCbCr ToYCbCr(in Cmyk color)
        {
            var rgb = this.ToRgb(color);

            return YCbCrAndRgbConverter.Convert(rgb);
        }

        /// <summary>
        /// Converts a <see cref="Hsl"/> into a <see cref="YCbCr"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="YCbCr"/></returns>
        public YCbCr ToYCbCr(in Hsl color)
        {
            var rgb = this.ToRgb(color);

            return YCbCrAndRgbConverter.Convert(rgb);
        }

        /// <summary>
        /// Converts a <see cref="Hsv"/> into a <see cref="YCbCr"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="YCbCr"/></returns>
        public YCbCr ToYCbCr(in Hsv color)
        {
            var rgb = this.ToRgb(color);

            return YCbCrAndRgbConverter.Convert(rgb);
        }

        /// <summary>
        /// Converts a <see cref="HunterLab"/> into a <see cref="YCbCr"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="YCbCr"/></returns>
        public YCbCr ToYCbCr(in HunterLab color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToYCbCr(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="LinearRgb"/> into a <see cref="YCbCr"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="YCbCr"/></returns>
        public YCbCr ToYCbCr(in LinearRgb color)
        {
            var rgb = this.ToRgb(color);

            return YCbCrAndRgbConverter.Convert(rgb);
        }

        /// <summary>
        /// Converts a <see cref="Lms"/> into a <see cref="YCbCr"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="YCbCr"/></returns>
        public YCbCr ToYCbCr(in Lms color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToYCbCr(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="Rgb"/> into a <see cref="YCbCr"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="YCbCr"/></returns>
        public YCbCr ToYCbCr(in Rgb color)
        {
            return YCbCrAndRgbConverter.Convert(color);
        }
    }
}