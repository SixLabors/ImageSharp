// <copyright file="ColorSpaceConverter.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.ColorSpaces.Conversion
{
    using ImageSharp.ColorSpaces.Conversion.Implementation.Rgb;

    /// <content>
    /// Allows conversion to <see cref="Rgb"/>.
    /// </content>
    internal partial class ColorSpaceConverter
    {
        private static readonly LinearRgbToRgbConverter LinearRgbToRgbConverter = new LinearRgbToRgbConverter();

        /// <summary>
        /// Converts a <see cref="CieLab"/> into a <see cref="Rgb"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Rgb"/></returns>
        public Rgb ToRgb(CieLab color)
        {
            Guard.NotNull(color, nameof(color));

            var xyzColor = this.ToCieXyz(color);
            return this.ToRgb(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieLch"/> into a <see cref="Rgb"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Rgb"/></returns>
        public Rgb ToRgb(CieLch color)
        {
            Guard.NotNull(color, nameof(color));

            var xyzColor = this.ToCieXyz(color);
            return this.ToRgb(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieLchuv"/> into a <see cref="Rgb"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Rgb"/></returns>
        public Rgb ToRgb(CieLchuv color)
        {
            Guard.NotNull(color, nameof(color));

            var xyzColor = this.ToCieXyz(color);
            return this.ToRgb(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieLuv"/> into a <see cref="Rgb"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Rgb"/></returns>
        public Rgb ToRgb(CieLuv color)
        {
            Guard.NotNull(color, nameof(color));

            var xyzColor = this.ToCieXyz(color);
            return this.ToRgb(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieXyy"/> into a <see cref="Rgb"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Rgb"/></returns>
        public Rgb ToRgb(CieXyy color)
        {
            Guard.NotNull(color, nameof(color));

            var xyzColor = this.ToCieXyz(color);
            return this.ToRgb(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieXyz"/> into a <see cref="Rgb"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Rgb"/></returns>
        public Rgb ToRgb(CieXyz color)
        {
            Guard.NotNull(color, nameof(color));

            // Conversion
            var linear = this.ToLinearRgb(color);

            // Compand
            return this.ToRgb(linear);
        }

        /// <summary>
        /// Converts a <see cref="Cmyk"/> into a <see cref="Rgb"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Rgb"/></returns>
        public Rgb ToRgb(Cmyk color)
        {
            Guard.NotNull(color, nameof(color));

            // Conversion
            return CmykAndRgbConverter.Convert(color);
        }

        /// <summary>
        /// Converts a <see cref="Hsv"/> into a <see cref="Rgb"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Rgb"/></returns>
        public Rgb ToRgb(Hsv color)
        {
            Guard.NotNull(color, nameof(color));

            // Conversion
            return HsvAndRgbConverter.Convert(color);
        }

        /// <summary>
        /// Converts a <see cref="Hsl"/> into a <see cref="Rgb"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Rgb"/></returns>
        public Rgb ToRgb(Hsl color)
        {
            Guard.NotNull(color, nameof(color));

            // Conversion
            return HslAndRgbConverter.Convert(color);
        }

        /// <summary>
        /// Converts a <see cref="HunterLab"/> into a <see cref="Rgb"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Rgb"/></returns>
        public Rgb ToRgb(HunterLab color)
        {
            Guard.NotNull(color, nameof(color));

            var xyzColor = this.ToCieXyz(color);
            return this.ToRgb(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="LinearRgb"/> into a <see cref="Rgb"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Rgb"/></returns>
        public Rgb ToRgb(LinearRgb color)
        {
            Guard.NotNull(color, nameof(color));

            // Conversion
            return LinearRgbToRgbConverter.Convert(color);
        }

        /// <summary>
        /// Converts a <see cref="Lms"/> into a <see cref="Rgb"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Rgb"/></returns>
        public Rgb ToRgb(Lms color)
        {
            Guard.NotNull(color, nameof(color));

            var xyzColor = this.ToCieXyz(color);
            return this.ToRgb(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="YCbCr"/> into a <see cref="Rgb"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Rgb"/></returns>
        public Rgb ToRgb(YCbCr color)
        {
            Guard.NotNull(color, nameof(color));

            // Conversion
            Rgb rgb = YCbCrAndRgbConverter.Convert(color);

            // Adaptation
            // TODO: Check this!
            return rgb.WorkingSpace.Equals(this.TargetRgbWorkingSpace) ? rgb : this.Adapt(rgb);
        }
    }
}