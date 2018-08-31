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
        public Hsl ToHsl(in CieLab color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToHsl(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieLch"/> into a <see cref="Hsl"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Hsl"/></returns>
        public Hsl ToHsl(in CieLch color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToHsl(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieLchuv"/> into a <see cref="Hsl"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Hsl"/></returns>
        public Hsl ToHsl(in CieLchuv color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToHsl(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieLuv"/> into a <see cref="Hsl"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Hsl"/></returns>
        public Hsl ToHsl(in CieLuv color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToHsl(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieXyy"/> into a <see cref="Hsl"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Hsl"/></returns>
        public Hsl ToHsl(in CieXyy color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToHsl(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieXyz"/> into a <see cref="Hsl"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Hsl"/></returns>
        public Hsl ToHsl(in CieXyz color)
        {
            var rgb = this.ToRgb(color);

            return HslAndRgbConverter.Convert(rgb);
        }

        /// <summary>
        /// Converts a <see cref="Cmyk"/> into a <see cref="Hsl"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Hsl"/></returns>
        public Hsl ToHsl(in Cmyk color)
        {
            var rgb = this.ToRgb(color);

            return HslAndRgbConverter.Convert(rgb);
        }

        /// <summary>
        /// Converts a <see cref="Hsv"/> into a <see cref="Hsl"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Hsl"/></returns>
        public Hsl ToHsl(in Hsv color)
        {
            var rgb = this.ToRgb(color);

            return HslAndRgbConverter.Convert(rgb);
        }

        /// <summary>
        /// Converts a <see cref="HunterLab"/> into a <see cref="Hsl"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Hsl"/></returns>
        public Hsl ToHsl(in HunterLab color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToHsl(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="LinearRgb"/> into a <see cref="Hsl"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Hsl"/></returns>
        public Hsl ToHsl(in LinearRgb color)
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
        public Hsl ToHsl(in Rgb color)
        {
            return HslAndRgbConverter.Convert(color);
        }

        /// <summary>
        /// Converts a <see cref="YCbCr"/> into a <see cref="Hsl"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Hsl"/></returns>
        public Hsl ToHsl(in YCbCr color)
        {
            var rgb = this.ToRgb(color);

            return HslAndRgbConverter.Convert(rgb);
        }
    }
}