// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

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
        public Cmyk ToCmyk(in CieLab color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToCmyk(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieLch"/> into a <see cref="Cmyk"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Cmyk"/></returns>
        public Cmyk ToCmyk(in CieLch color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToCmyk(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieLchuv"/> into a <see cref="Cmyk"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Cmyk"/></returns>
        public Cmyk ToCmyk(in CieLchuv color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToCmyk(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieLuv"/> into a <see cref="Cmyk"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Cmyk"/></returns>
        public Cmyk ToCmyk(in CieLuv color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToCmyk(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieXyy"/> into a <see cref="Cmyk"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Cmyk"/></returns>
        public Cmyk ToCmyk(in CieXyy color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToCmyk(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieXyz"/> into a <see cref="Cmyk"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Cmyk"/></returns>
        public Cmyk ToCmyk(in CieXyz color)
        {
            var rgb = this.ToRgb(color);

            return CmykAndRgbConverter.Convert(rgb);
        }

        /// <summary>
        /// Converts a <see cref="Hsl"/> into a <see cref="Cmyk"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Cmyk"/></returns>
        public Cmyk ToCmyk(in Hsl color)
        {
            var rgb = this.ToRgb(color);

            return CmykAndRgbConverter.Convert(rgb);
        }

        /// <summary>
        /// Converts a <see cref="Hsv"/> into a <see cref="Cmyk"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Cmyk"/></returns>
        public Cmyk ToCmyk(in Hsv color)
        {
            var rgb = this.ToRgb(color);

            return CmykAndRgbConverter.Convert(rgb);
        }

        /// <summary>
        /// Converts a <see cref="HunterLab"/> into a <see cref="Cmyk"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Cmyk"/></returns>
        public Cmyk ToCmyk(in HunterLab color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToCmyk(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="LinearRgb"/> into a <see cref="Cmyk"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Cmyk"/></returns>
        public Cmyk ToCmyk(in LinearRgb color)
        {
            var rgb = this.ToRgb(color);

            return CmykAndRgbConverter.Convert(rgb);
        }

        /// <summary>
        /// Converts a <see cref="Lms"/> into a <see cref="Cmyk"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Cmyk"/></returns>
        public Cmyk ToCmyk(in Lms color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToCmyk(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="Rgb"/> into a <see cref="Cmyk"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Cmyk"/></returns>
        public Cmyk ToCmyk(in Rgb color)
        {
            return CmykAndRgbConverter.Convert(color);
        }

        /// <summary>
        /// Converts a <see cref="YCbCr"/> into a <see cref="Cmyk"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Cmyk"/></returns>
        public Cmyk ToCmyk(in YCbCr color)
        {
            var rgb = this.ToRgb(color);

            return CmykAndRgbConverter.Convert(rgb);
        }
    }
}