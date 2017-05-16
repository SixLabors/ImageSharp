// <copyright file="ColorSpaceConverter.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.ColorSpaces.Conversion
{
    using ImageSharp.ColorSpaces.Conversion.Implementation.CieXyy;

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
            Guard.NotNull(color, nameof(color));

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
            Guard.NotNull(color, nameof(color));

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
            Guard.NotNull(color, nameof(color));

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
            Guard.NotNull(color, nameof(color));

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
            Guard.NotNull(color, nameof(color));

            return CieXyzAndCieXyyConverter.Convert(color);
        }

        /// <summary>
        /// Converts a <see cref="Cmyk"/> into a <see cref="CieXyy"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieXyy"/></returns>
        public CieXyy ToCieXyy(Cmyk color)
        {
            Guard.NotNull(color, nameof(color));

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
            Guard.NotNull(color, nameof(color));

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
            Guard.NotNull(color, nameof(color));

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
            Guard.NotNull(color, nameof(color));

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
            Guard.NotNull(color, nameof(color));

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
            Guard.NotNull(color, nameof(color));

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
            Guard.NotNull(color, nameof(color));

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
            Guard.NotNull(color, nameof(color));

            var xyzColor = this.ToCieXyz(color);

            return this.ToCieXyy(xyzColor);
        }
    }
}