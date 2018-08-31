// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation.HsvColorSapce;

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
        public Hsv ToHsv(in CieLab color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToHsv(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieLch"/> into a <see cref="Hsv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Hsv"/></returns>
        public Hsv ToHsv(in CieLch color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToHsv(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieLchuv"/> into a <see cref="Hsv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Hsv"/></returns>
        public Hsv ToHsv(in CieLchuv color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToHsv(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieLuv"/> into a <see cref="Hsv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Hsv"/></returns>
        public Hsv ToHsv(in CieLuv color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToHsv(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieXyy"/> into a <see cref="Hsv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Hsv"/></returns>
        public Hsv ToHsv(in CieXyy color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToHsv(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieXyz"/> into a <see cref="Hsv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Hsv"/></returns>
        public Hsv ToHsv(in CieXyz color)
        {
            var rgb = this.ToRgb(color);

            return HsvAndRgbConverter.Convert(rgb);
        }

        /// <summary>
        /// Converts a <see cref="Cmyk"/> into a <see cref="Hsv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Hsv"/></returns>
        public Hsv ToHsv(in Cmyk color)
        {
            var rgb = this.ToRgb(color);

            return HsvAndRgbConverter.Convert(rgb);
        }

        /// <summary>
        /// Converts a <see cref="Hsl"/> into a <see cref="Hsv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Hsv"/></returns>
        public Hsv ToHsv(in Hsl color)
        {
            var rgb = this.ToRgb(color);

            return HsvAndRgbConverter.Convert(rgb);
        }

        /// <summary>
        /// Converts a <see cref="HunterLab"/> into a <see cref="Hsv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Hsv"/></returns>
        public Hsv ToHsv(in HunterLab color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToHsv(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="LinearRgb"/> into a <see cref="Hsv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Hsv"/></returns>
        public Hsv ToHsv(in LinearRgb color)
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
        public Hsv ToHsv(in Rgb color)
        {
            return HsvAndRgbConverter.Convert(color);
        }

        /// <summary>
        /// Converts a <see cref="YCbCr"/> into a <see cref="Hsv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Hsv"/></returns>
        public Hsv ToHsv(in YCbCr color)
        {
            var rgb = this.ToRgb(color);

            return HsvAndRgbConverter.Convert(rgb);
        }
    }
}