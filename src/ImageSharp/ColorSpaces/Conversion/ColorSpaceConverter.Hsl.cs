// <copyright file="ColorSpaceConverter.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.ColorSpaces.Conversion
{
    using ImageSharp.ColorSpaces;
    using ImageSharp.ColorSpaces.Conversion.Implementation.Hsl;

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
            Guard.NotNull(color, nameof(color));

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
            Guard.NotNull(color, nameof(color));

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
            Guard.NotNull(color, nameof(color));

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
            Guard.NotNull(color, nameof(color));

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
            Guard.NotNull(color, nameof(color));

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
            Guard.NotNull(color, nameof(color));

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
            Guard.NotNull(color, nameof(color));

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
            Guard.NotNull(color, nameof(color));

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
            Guard.NotNull(color, nameof(color));

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
            Guard.NotNull(color, nameof(color));

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
            Guard.NotNull(color, nameof(color));

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
            Guard.NotNull(color, nameof(color));

            return HslAndRgbConverter.Convert(color);
        }

        /// <summary>
        /// Converts a <see cref="YCbCr"/> into a <see cref="Hsl"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Hsl"/></returns>
        public Hsl ToHsl(YCbCr color)
        {
            Guard.NotNull(color, nameof(color));

            var rgb = this.ToRgb(color);

            return HslAndRgbConverter.Convert(rgb);
        }
    }
}