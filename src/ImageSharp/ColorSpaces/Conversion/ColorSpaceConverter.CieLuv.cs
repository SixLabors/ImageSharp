// <copyright file="ColorSpaceConverter.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.ColorSpaces.Conversion
{
    using ImageSharp.ColorSpaces;
    using ImageSharp.ColorSpaces.Conversion.Implementation.CieLuv;

    /// <summary>
    /// Converts between color spaces ensuring that the color is adapted using chromatic adaptation.
    /// </summary>
    public partial class ColorSpaceConverter
    {
        /// <summary>
        /// Converts a <see cref="CieLab"/> into a <see cref="CieLuv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLuv"/></returns>
        public CieLuv ToCieLuv(CieLab color)
        {
            Guard.NotNull(color, nameof(color));

            CieXyz xyzColor = this.ToCieXyz(color);
            return this.ToCieLuv(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieLch"/> into a <see cref="CieLuv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLuv"/></returns>
        public CieLuv ToCieLuv(CieLch color)
        {
            Guard.NotNull(color, nameof(color));

            CieXyz xyzColor = this.ToCieXyz(color);
            return this.ToCieLuv(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieXyy"/> into a <see cref="CieLuv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLuv"/></returns>
        public CieLuv ToCieLuv(CieXyy color)
        {
            Guard.NotNull(color, nameof(color));

            CieXyz xyzColor = this.ToCieXyz(color);
            return this.ToCieLuv(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieXyz"/> into a <see cref="CieLuv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLuv"/></returns>
        public CieLuv ToCieLuv(CieXyz color)
        {
            Guard.NotNull(color, nameof(color));

            // Adaptation
            CieXyz adapted = !this.WhitePoint.Equals(this.TargetLabWhitePoint) && this.IsChromaticAdaptationPerformed
                ? this.ChromaticAdaptation.Transform(color, this.WhitePoint, this.TargetLabWhitePoint)
                : color;

            // Conversion
            CieXyzToCieLuvConverter converter = new CieXyzToCieLuvConverter(this.TargetLuvWhitePoint);
            return converter.Convert(adapted);
        }

        /// <summary>
        /// Converts a <see cref="Cmyk"/> into a <see cref="CieLuv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLuv"/></returns>
        public CieLuv ToCieLuv(Cmyk color)
        {
            Guard.NotNull(color, nameof(color));

            CieXyz xyzColor = this.ToCieXyz(color);
            return this.ToCieLuv(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="Hsl"/> into a <see cref="CieLuv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLuv"/></returns>
        public CieLuv ToCieLuv(Hsl color)
        {
            Guard.NotNull(color, nameof(color));

            CieXyz xyzColor = this.ToCieXyz(color);
            return this.ToCieLuv(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="Hsv"/> into a <see cref="CieLuv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLuv"/></returns>
        public CieLuv ToCieLuv(Hsv color)
        {
            Guard.NotNull(color, nameof(color));

            CieXyz xyzColor = this.ToCieXyz(color);
            return this.ToCieLuv(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="HunterLab"/> into a <see cref="CieLuv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLuv"/></returns>
        public CieLuv ToCieLuv(HunterLab color)
        {
            Guard.NotNull(color, nameof(color));

            CieXyz xyzColor = this.ToCieXyz(color);
            return this.ToCieLuv(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="Lms"/> into a <see cref="CieLuv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLuv"/></returns>
        public CieLuv ToCieLuv(Lms color)
        {
            Guard.NotNull(color, nameof(color));

            CieXyz xyzColor = this.ToCieXyz(color);
            return this.ToCieLuv(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="LinearRgb"/> into a <see cref="CieLuv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLuv"/></returns>
        public CieLuv ToCieLuv(LinearRgb color)
        {
            Guard.NotNull(color, nameof(color));

            CieXyz xyzColor = this.ToCieXyz(color);
            return this.ToCieLuv(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="Rgb"/> into a <see cref="CieLuv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLuv"/></returns>
        public CieLuv ToCieLuv(Rgb color)
        {
            Guard.NotNull(color, nameof(color));

            CieXyz xyzColor = this.ToCieXyz(color);
            return this.ToCieLuv(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="YCbCr"/> into a <see cref="CieLuv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLuv"/></returns>
        public CieLuv ToCieLuv(YCbCr color)
        {
            Guard.NotNull(color, nameof(color));

            CieXyz xyzColor = this.ToCieXyz(color);
            return this.ToCieLuv(xyzColor);
        }
    }
}