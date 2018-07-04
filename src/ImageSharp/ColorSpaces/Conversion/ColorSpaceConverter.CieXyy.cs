// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion
{
    /// <content>
    /// Allows conversion to <see cref="CieXyy"/>.
    /// </content>
    internal partial class ColorSpaceConverter
    {
        private static readonly CieXyzAndCieXyyConverter CieXyzAndCieXyyConverter = new CieXyzAndCieXyyConverter();

        /// <summary>
        /// Converts a <see cref="CieLab"/> into a <see cref="CieXyy"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieXyy"/></returns>
        public CieXyy ToCieXyy(CieLab color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToCieXyy(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieLch"/> into a <see cref="CieXyy"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieXyy"/></returns>
        public CieXyy ToCieXyy(CieLch color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToCieXyy(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieLchuv"/> into a <see cref="CieXyy"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieXyy"/></returns>
        public CieXyy ToCieXyy(CieLchuv color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToCieXyy(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieLuv"/> into a <see cref="CieXyy"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieXyy"/></returns>
        public CieXyy ToCieXyy(CieLuv color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToCieXyy(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieXyz"/> into a <see cref="CieXyy"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieXyy"/></returns>
        public CieXyy ToCieXyy(CieXyz color)
        {
            return CieXyzAndCieXyyConverter.Convert(color);
        }

        /// <summary>
        /// Converts a <see cref="Cmyk"/> into a <see cref="CieXyy"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieXyy"/></returns>
        public CieXyy ToCieXyy(Cmyk color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToCieXyy(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="Hsl"/> into a <see cref="CieXyy"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieXyy"/></returns>
        public CieXyy ToCieXyy(Hsl color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToCieXyy(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="Hsv"/> into a <see cref="CieXyy"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieXyy"/></returns>
        public CieXyy ToCieXyy(Hsv color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToCieXyy(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="HunterLab"/> into a <see cref="CieXyy"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieXyy"/></returns>
        public CieXyy ToCieXyy(HunterLab color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToCieXyy(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="LinearRgb"/> into a <see cref="CieXyy"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieXyy"/></returns>
        public CieXyy ToCieXyy(LinearRgb color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToCieXyy(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="Lms"/> into a <see cref="CieXyy"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieXyy"/></returns>
        public CieXyy ToCieXyy(Lms color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToCieXyy(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="Rgb"/> into a <see cref="CieXyy"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieXyy"/></returns>
        public CieXyy ToCieXyy(Rgb color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToCieXyy(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="YCbCr"/> into a <see cref="CieXyy"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieXyy"/></returns>
        public CieXyy ToCieXyy(YCbCr color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToCieXyy(xyzColor);
        }
    }
}