// <copyright file="ColorSpaceConverter.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.ColorSpaces.Conversion
{
    using ImageSharp.ColorSpaces;
    using ImageSharp.ColorSpaces.Conversion.Implementation.YCbCr;

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
        public YCbCr ToYCbCr(CieLab color)
        {
            Guard.NotNull(color, nameof(color));

            var xyzColor = this.ToCieXyz(color);

            return this.ToYCbCr(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieLch"/> into a <see cref="YCbCr"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="YCbCr"/></returns>
        public YCbCr ToYCbCr(CieLch color)
        {
            Guard.NotNull(color, nameof(color));

            var xyzColor = this.ToCieXyz(color);

            return this.ToYCbCr(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieLchuv"/> into a <see cref="YCbCr"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="YCbCr"/></returns>
        public YCbCr ToYCbCr(CieLchuv color)
        {
            Guard.NotNull(color, nameof(color));

            var xyzColor = this.ToCieXyz(color);

            return this.ToYCbCr(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieLuv"/> into a <see cref="YCbCr"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="YCbCr"/></returns>
        public YCbCr ToYCbCr(CieLuv color)
        {
            Guard.NotNull(color, nameof(color));

            var xyzColor = this.ToCieXyz(color);

            return this.ToYCbCr(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieXyy"/> into a <see cref="YCbCr"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="YCbCr"/></returns>
        public YCbCr ToYCbCr(CieXyy color)
        {
            Guard.NotNull(color, nameof(color));

            var xyzColor = this.ToCieXyz(color);

            return this.ToYCbCr(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieXyz"/> into a <see cref="YCbCr"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="YCbCr"/></returns>
        public YCbCr ToYCbCr(CieXyz color)
        {
            Guard.NotNull(color, nameof(color));

            var rgb = this.ToRgb(color);

            return YCbCrAndRgbConverter.Convert(rgb);
        }

        /// <summary>
        /// Converts a <see cref="Cmyk"/> into a <see cref="YCbCr"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="YCbCr"/></returns>
        public YCbCr ToYCbCr(Cmyk color)
        {
            Guard.NotNull(color, nameof(color));

            var rgb = this.ToRgb(color);

            return YCbCrAndRgbConverter.Convert(rgb);
        }

        /// <summary>
        /// Converts a <see cref="Hsl"/> into a <see cref="YCbCr"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="YCbCr"/></returns>
        public YCbCr ToYCbCr(Hsl color)
        {
            Guard.NotNull(color, nameof(color));

            var rgb = this.ToRgb(color);

            return YCbCrAndRgbConverter.Convert(rgb);
        }

        /// <summary>
        /// Converts a <see cref="Hsv"/> into a <see cref="YCbCr"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="YCbCr"/></returns>
        public YCbCr ToYCbCr(Hsv color)
        {
            Guard.NotNull(color, nameof(color));

            var rgb = this.ToRgb(color);

            return YCbCrAndRgbConverter.Convert(rgb);
        }

        /// <summary>
        /// Converts a <see cref="HunterLab"/> into a <see cref="YCbCr"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="YCbCr"/></returns>
        public YCbCr ToYCbCr(HunterLab color)
        {
            Guard.NotNull(color, nameof(color));

            var xyzColor = this.ToCieXyz(color);

            return this.ToYCbCr(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="LinearRgb"/> into a <see cref="YCbCr"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="YCbCr"/></returns>
        public YCbCr ToYCbCr(LinearRgb color)
        {
            Guard.NotNull(color, nameof(color));

            var rgb = this.ToRgb(color);

            return YCbCrAndRgbConverter.Convert(rgb);
        }

        /// <summary>
        /// Converts a <see cref="Lms"/> into a <see cref="YCbCr"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="YCbCr"/></returns>
        public YCbCr ToYCbCr(Lms color)
        {
            Guard.NotNull(color, nameof(color));

            var xyzColor = this.ToCieXyz(color);

            return this.ToYCbCr(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="Rgb"/> into a <see cref="YCbCr"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="YCbCr"/></returns>
        public YCbCr ToYCbCr(Rgb color)
        {
            Guard.NotNull(color, nameof(color));

            return YCbCrAndRgbConverter.Convert(color);
        }
    }
}