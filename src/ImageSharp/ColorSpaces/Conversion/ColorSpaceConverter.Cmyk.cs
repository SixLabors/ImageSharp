// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation.CmykColorSapce;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion
{
    /// <content>
    /// Allows conversion to <see cref="Cmyk"/>.
    /// </content>
    internal partial class ColorSpaceConverter
    {
        private static readonly CmykAndRgbConverter CmykAndRgbConverter = new CmykAndRgbConverter();

        /// <summary>
        /// Converts a <see cref="CieLab"/> into a <see cref="Cmyk"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Cmyk"/></returns>
        public Cmyk ToCmyk(CieLab color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToCmyk(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieLch"/> into a <see cref="Cmyk"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Cmyk"/></returns>
        public Cmyk ToCmyk(CieLch color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToCmyk(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieLchuv"/> into a <see cref="Cmyk"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Cmyk"/></returns>
        public Cmyk ToCmyk(CieLchuv color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToCmyk(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieLuv"/> into a <see cref="Cmyk"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Cmyk"/></returns>
        public Cmyk ToCmyk(CieLuv color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToCmyk(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieXyy"/> into a <see cref="Cmyk"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Cmyk"/></returns>
        public Cmyk ToCmyk(CieXyy color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToCmyk(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieXyz"/> into a <see cref="Cmyk"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Cmyk"/></returns>
        public Cmyk ToCmyk(CieXyz color)
        {
            var rgb = this.ToRgb(color);

            return CmykAndRgbConverter.Convert(rgb);
        }

        /// <summary>
        /// Converts a <see cref="Hsl"/> into a <see cref="Cmyk"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Cmyk"/></returns>
        public Cmyk ToCmyk(Hsl color)
        {
            var rgb = this.ToRgb(color);

            return CmykAndRgbConverter.Convert(rgb);
        }

        /// <summary>
        /// Converts a <see cref="Hsv"/> into a <see cref="Cmyk"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Cmyk"/></returns>
        public Cmyk ToCmyk(Hsv color)
        {
            var rgb = this.ToRgb(color);

            return CmykAndRgbConverter.Convert(rgb);
        }

        /// <summary>
        /// Converts a <see cref="HunterLab"/> into a <see cref="Cmyk"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Cmyk"/></returns>
        public Cmyk ToCmyk(HunterLab color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToCmyk(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="LinearRgb"/> into a <see cref="Cmyk"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Cmyk"/></returns>
        public Cmyk ToCmyk(LinearRgb color)
        {
            var rgb = this.ToRgb(color);

            return CmykAndRgbConverter.Convert(rgb);
        }

        /// <summary>
        /// Converts a <see cref="Lms"/> into a <see cref="Cmyk"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Cmyk"/></returns>
        public Cmyk ToCmyk(Lms color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToCmyk(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="Rgb"/> into a <see cref="Cmyk"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Cmyk"/></returns>
        public Cmyk ToCmyk(Rgb color)
        {
            return CmykAndRgbConverter.Convert(color);
        }

        /// <summary>
        /// Converts a <see cref="YCbCr"/> into a <see cref="Cmyk"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Cmyk"/></returns>
        public Cmyk ToCmyk(YCbCr color)
        {
            var rgb = this.ToRgb(color);

            return CmykAndRgbConverter.Convert(rgb);
        }
    }
}