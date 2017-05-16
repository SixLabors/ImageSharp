// <copyright file="ColorSpaceConverter.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.ColorSpaces.Conversion
{
    using ImageSharp.ColorSpaces;

    /// <content>
    /// Allows conversion to <see cref="Lms"/>.
    /// </content>
    internal partial class ColorSpaceConverter
    {
        /// <summary>
        /// Converts a <see cref="CieLab"/> into a <see cref="Lms"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Lms"/></returns>
        public Lms ToLms(CieLab color)
        {
            Guard.NotNull(color, nameof(color));

            var xyzColor = this.ToCieXyz(color);
            return this.ToLms(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieLch"/> into a <see cref="Lms"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Lms"/></returns>
        public Lms ToLms(CieLch color)
        {
            Guard.NotNull(color, nameof(color));

            var xyzColor = this.ToCieXyz(color);
            return this.ToLms(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieLchuv"/> into a <see cref="Lms"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Lms"/></returns>
        public Lms ToLms(CieLchuv color)
        {
            Guard.NotNull(color, nameof(color));

            var xyzColor = this.ToCieXyz(color);
            return this.ToLms(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieLuv"/> into a <see cref="Lms"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Lms"/></returns>
        public Lms ToLms(CieLuv color)
        {
            Guard.NotNull(color, nameof(color));

            var xyzColor = this.ToCieXyz(color);
            return this.ToLms(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieXyy"/> into a <see cref="Lms"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Lms"/></returns>
        public Lms ToLms(CieXyy color)
        {
            Guard.NotNull(color, nameof(color));

            var xyzColor = this.ToCieXyz(color);
            return this.ToLms(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieXyz"/> into a <see cref="Lms"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Lms"/></returns>
        public Lms ToLms(CieXyz color)
        {
            Guard.NotNull(color, nameof(color));

            // Conversion
            return this.cachedCieXyzAndLmsConverter.Convert(color);
        }

        /// <summary>
        /// Converts a <see cref="Cmyk"/> into a <see cref="Lms"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Lms"/></returns>
        public Lms ToLms(Cmyk color)
        {
            Guard.NotNull(color, nameof(color));

            var xyzColor = this.ToCieXyz(color);
            return this.ToLms(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="Hsl"/> into a <see cref="Lms"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Lms"/></returns>
        public Lms ToLms(Hsl color)
        {
            Guard.NotNull(color, nameof(color));

            var xyzColor = this.ToCieXyz(color);
            return this.ToLms(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="Hsv"/> into a <see cref="Lms"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Lms"/></returns>
        public Lms ToLms(Hsv color)
        {
            Guard.NotNull(color, nameof(color));

            var xyzColor = this.ToCieXyz(color);
            return this.ToLms(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="HunterLab"/> into a <see cref="Lms"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Lms"/></returns>
        public Lms ToLms(HunterLab color)
        {
            Guard.NotNull(color, nameof(color));

            var xyzColor = this.ToCieXyz(color);
            return this.ToLms(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="LinearRgb"/> into a <see cref="Lms"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Lms"/></returns>
        public Lms ToLms(LinearRgb color)
        {
            Guard.NotNull(color, nameof(color));

            var xyzColor = this.ToCieXyz(color);
            return this.ToLms(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="Rgb"/> into a <see cref="Lms"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Lms"/></returns>
        public Lms ToLms(Rgb color)
        {
            Guard.NotNull(color, nameof(color));

            var xyzColor = this.ToCieXyz(color);
            return this.ToLms(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="YCbCr"/> into a <see cref="Lms"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Lms"/></returns>
        public Lms ToLms(YCbCr color)
        {
            Guard.NotNull(color, nameof(color));

            var xyzColor = this.ToCieXyz(color);
            return this.ToLms(xyzColor);
        }
    }
}