// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion
{
    /// <content>
    /// Allows conversion to <see cref="Hsv"/>.
    /// </content>
    internal partial class ColorSpaceConverter
    {
        private static readonly HsvAndRgbConverter HsvAndRgbConverter = new HsvAndRgbConverter();

        /// <summary>
        /// Converts a <see cref="CieLab"/> into a <see cref="Hsv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Hsv"/></returns>
        public Hsv ToHsv(CieLab color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToHsv(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieLch"/> into a <see cref="Hsv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Hsv"/></returns>
        public Hsv ToHsv(CieLch color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToHsv(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieLchuv"/> into a <see cref="Hsv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Hsv"/></returns>
        public Hsv ToHsv(CieLchuv color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToHsv(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieLuv"/> into a <see cref="Hsv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Hsv"/></returns>
        public Hsv ToHsv(CieLuv color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToHsv(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieXyy"/> into a <see cref="Hsv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Hsv"/></returns>
        public Hsv ToHsv(CieXyy color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToHsv(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieXyz"/> into a <see cref="Hsv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Hsv"/></returns>
        public Hsv ToHsv(CieXyz color)
        {
            var rgb = this.ToRgb(color);

            return HsvAndRgbConverter.Convert(rgb);
        }

        /// <summary>
        /// Converts a <see cref="Cmyk"/> into a <see cref="Hsv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Hsv"/></returns>
        public Hsv ToHsv(Cmyk color)
        {
            var rgb = this.ToRgb(color);

            return HsvAndRgbConverter.Convert(rgb);
        }

        /// <summary>
        /// Converts a <see cref="Hsl"/> into a <see cref="Hsv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Hsv"/></returns>
        public Hsv ToHsv(Hsl color)
        {
            var rgb = this.ToRgb(color);

            return HsvAndRgbConverter.Convert(rgb);
        }

        /// <summary>
        /// Converts a <see cref="HunterLab"/> into a <see cref="Hsv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Hsv"/></returns>
        public Hsv ToHsv(HunterLab color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToHsv(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="LinearRgb"/> into a <see cref="Hsv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Hsv"/></returns>
        public Hsv ToHsv(LinearRgb color)
        {
            var rgb = this.ToRgb(color);

            return HsvAndRgbConverter.Convert(rgb);
        }

        /// <summary>
        /// Converts a <see cref="Lms"/> into a <see cref="Hsv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Hsv"/></returns>
        public Hsv ToHsv(Lms color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToHsv(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="Rgb"/> into a <see cref="Hsv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Hsv"/></returns>
        public Hsv ToHsv(Rgb color)
        {
            return HsvAndRgbConverter.Convert(color);
        }

        /// <summary>
        /// Converts a <see cref="YCbCr"/> into a <see cref="Hsv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Hsv"/></returns>
        public Hsv ToHsv(YCbCr color)
        {
            var rgb = this.ToRgb(color);

            return HsvAndRgbConverter.Convert(rgb);
        }
    }
}