// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation.HslColorSapce;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion
{
    /// <content>
    /// Allows conversion to <see cref="Hsl"/>.
    /// </content>
    internal partial class ColorSpaceConverter
    {
        private static readonly HslAndRgbConverter HslAndRgbConverter = new HslAndRgbConverter();

        /// <summary>
        /// Converts a <see cref="CieLab"/> into a <see cref="Hsl"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Hsl"/></returns>
        public Hsl ToHsl(CieLab color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToHsl(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieLch"/> into a <see cref="Hsl"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Hsl"/></returns>
        public Hsl ToHsl(CieLch color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToHsl(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieLchuv"/> into a <see cref="Hsl"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Hsl"/></returns>
        public Hsl ToHsl(CieLchuv color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToHsl(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieLuv"/> into a <see cref="Hsl"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Hsl"/></returns>
        public Hsl ToHsl(CieLuv color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToHsl(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieXyy"/> into a <see cref="Hsl"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Hsl"/></returns>
        public Hsl ToHsl(CieXyy color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToHsl(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieXyz"/> into a <see cref="Hsl"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Hsl"/></returns>
        public Hsl ToHsl(CieXyz color)
        {
            var rgb = this.ToRgb(color);

            return HslAndRgbConverter.Convert(rgb);
        }

        /// <summary>
        /// Converts a <see cref="Cmyk"/> into a <see cref="Hsl"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Hsl"/></returns>
        public Hsl ToHsl(Cmyk color)
        {
            var rgb = this.ToRgb(color);

            return HslAndRgbConverter.Convert(rgb);
        }

        /// <summary>
        /// Converts a <see cref="Hsv"/> into a <see cref="Hsl"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Hsl"/></returns>
        public Hsl ToHsl(Hsv color)
        {
            var rgb = this.ToRgb(color);

            return HslAndRgbConverter.Convert(rgb);
        }

        /// <summary>
        /// Converts a <see cref="HunterLab"/> into a <see cref="Hsl"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Hsl"/></returns>
        public Hsl ToHsl(HunterLab color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToHsl(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="LinearRgb"/> into a <see cref="Hsl"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Hsl"/></returns>
        public Hsl ToHsl(LinearRgb color)
        {
            var rgb = this.ToRgb(color);

            return HslAndRgbConverter.Convert(rgb);
        }

        /// <summary>
        /// Converts a <see cref="Lms"/> into a <see cref="Hsl"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Hsl"/></returns>
        public Hsl ToHsl(Lms color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToHsl(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="Rgb"/> into a <see cref="Hsl"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Hsl"/></returns>
        public Hsl ToHsl(Rgb color)
        {
            return HslAndRgbConverter.Convert(color);
        }

        /// <summary>
        /// Converts a <see cref="YCbCr"/> into a <see cref="Hsl"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Hsl"/></returns>
        public Hsl ToHsl(YCbCr color)
        {
            var rgb = this.ToRgb(color);

            return HslAndRgbConverter.Convert(rgb);
        }
    }
}