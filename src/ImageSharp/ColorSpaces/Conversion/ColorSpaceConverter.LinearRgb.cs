// <copyright file="ColorSpaceConverter.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.ColorSpaces.Conversion
{
    using ImageSharp.ColorSpaces.Conversion.Implementation.Rgb;

    /// <content>
    /// Allows conversion to <see cref="LinearRgb"/>.
    /// </content>
    internal partial class ColorSpaceConverter
    {
        private static readonly RgbToLinearRgbConverter RgbToLinearRgbConverter = new RgbToLinearRgbConverter();

        private CieXyzToLinearRgbConverter cieXyzToLinearRgbConverter;

        /// <summary>
        /// Converts a <see cref="CieLab"/> into a <see cref="LinearRgb"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="LinearRgb"/></returns>
        public LinearRgb ToLinearRgb(CieLab color)
        {
            Guard.NotNull(color, nameof(color));

            var xyzColor = this.ToCieXyz(color);
            return this.ToLinearRgb(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieLch"/> into a <see cref="LinearRgb"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="LinearRgb"/></returns>
        public LinearRgb ToLinearRgb(CieLch color)
        {
            Guard.NotNull(color, nameof(color));

            var xyzColor = this.ToCieXyz(color);
            return this.ToLinearRgb(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieLchuv"/> into a <see cref="LinearRgb"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="LinearRgb"/></returns>
        public LinearRgb ToLinearRgb(CieLchuv color)
        {
            Guard.NotNull(color, nameof(color));

            var xyzColor = this.ToCieXyz(color);
            return this.ToLinearRgb(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieLuv"/> into a <see cref="LinearRgb"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="LinearRgb"/></returns>
        public LinearRgb ToLinearRgb(CieLuv color)
        {
            Guard.NotNull(color, nameof(color));

            var xyzColor = this.ToCieXyz(color);
            return this.ToLinearRgb(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieXyy"/> into a <see cref="LinearRgb"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="LinearRgb"/></returns>
        public LinearRgb ToLinearRgb(CieXyy color)
        {
            Guard.NotNull(color, nameof(color));

            var xyzColor = this.ToCieXyz(color);
            return this.ToLinearRgb(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieXyz"/> into a <see cref="LinearRgb"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="LinearRgb"/></returns>
        public LinearRgb ToLinearRgb(CieXyz color)
        {
            Guard.NotNull(color, nameof(color));

            // Adaptation
            CieXyz adapted = this.TargetRgbWorkingSpace.WhitePoint.Equals(this.WhitePoint) || !this.IsChromaticAdaptationPerformed
                ? color
                : this.ChromaticAdaptation.Transform(color, this.WhitePoint, this.TargetRgbWorkingSpace.WhitePoint);

            // Conversion
            CieXyzToLinearRgbConverter xyzConverter = this.GetCieXyxToLinearRgbConverter(this.TargetRgbWorkingSpace);
            return xyzConverter.Convert(adapted);
        }

        /// <summary>
        /// Converts a <see cref="Cmyk"/> into a <see cref="LinearRgb"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="LinearRgb"/></returns>
        public LinearRgb ToLinearRgb(Cmyk color)
        {
            Guard.NotNull(color, nameof(color));

            var rgb = this.ToRgb(color);
            return this.ToLinearRgb(rgb);
        }

        /// <summary>
        /// Converts a <see cref="Hsl"/> into a <see cref="LinearRgb"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="LinearRgb"/></returns>
        public LinearRgb ToLinearRgb(Hsl color)
        {
            Guard.NotNull(color, nameof(color));

            var rgb = this.ToRgb(color);
            return this.ToLinearRgb(rgb);
        }

        /// <summary>
        /// Converts a <see cref="Hsv"/> into a <see cref="LinearRgb"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="LinearRgb"/></returns>
        public LinearRgb ToLinearRgb(Hsv color)
        {
            Guard.NotNull(color, nameof(color));

            var rgb = this.ToRgb(color);
            return this.ToLinearRgb(rgb);
        }

        /// <summary>
        /// Converts a <see cref="HunterLab"/> into a <see cref="LinearRgb"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="LinearRgb"/></returns>
        public LinearRgb ToLinearRgb(HunterLab color)
        {
            Guard.NotNull(color, nameof(color));

            var xyzColor = this.ToCieXyz(color);
            return this.ToLinearRgb(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="Lms"/> into a <see cref="LinearRgb"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="LinearRgb"/></returns>
        public LinearRgb ToLinearRgb(Lms color)
        {
            Guard.NotNull(color, nameof(color));

            var xyzColor = this.ToCieXyz(color);
            return this.ToLinearRgb(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="Rgb"/> into a <see cref="LinearRgb"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="LinearRgb"/></returns>
        public LinearRgb ToLinearRgb(Rgb color)
        {
            Guard.NotNull(color, nameof(color));

            // Conversion
            return RgbToLinearRgbConverter.Convert(color);
        }

        /// <summary>
        /// Converts a <see cref="YCbCr"/> into a <see cref="LinearRgb"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="LinearRgb"/></returns>
        public LinearRgb ToLinearRgb(YCbCr color)
        {
            Guard.NotNull(color, nameof(color));

            var rgb = this.ToRgb(color);
            return this.ToLinearRgb(rgb);
        }

        /// <summary>
        /// Gets the correct converter for the given rgb working space.
        /// </summary>
        /// <param name="workingSpace">The target working space</param>
        /// <returns>The <see cref="CieXyzToLinearRgbConverter"/></returns>
        private CieXyzToLinearRgbConverter GetCieXyxToLinearRgbConverter(IRgbWorkingSpace workingSpace)
        {
            if (this.cieXyzToLinearRgbConverter != null && this.cieXyzToLinearRgbConverter.TargetWorkingSpace.Equals(workingSpace))
            {
                return this.cieXyzToLinearRgbConverter;
            }

            return this.cieXyzToLinearRgbConverter = new CieXyzToLinearRgbConverter(workingSpace);
        }
    }
}