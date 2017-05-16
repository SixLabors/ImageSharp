// <copyright file="ColorSpaceConverter.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.ColorSpaces.Conversion
{
    using ImageSharp.ColorSpaces;
    using ImageSharp.ColorSpaces.Conversion.Implementation.CieLchuv;
    using ImageSharp.ColorSpaces.Conversion.Implementation.CieLuv;

    /// <content>
    /// Allows conversion to <see cref="CieLuv"/>.
    /// </content>
    internal partial class ColorSpaceConverter
    {
        private static readonly CieLchuvToCieLuvConverter CieLchuvToCieLuvConverter = new CieLchuvToCieLuvConverter();

        /// <summary>
        /// Converts a <see cref="CieLab"/> into a <see cref="CieLuv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLuv"/></returns>
        public CieLuv ToCieLuv(CieLab color)
        {
            Guard.NotNull(color, nameof(color));

            var xyzColor = this.ToCieXyz(color);
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

            var xyzColor = this.ToCieXyz(color);
            return this.ToCieLuv(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieLchuv"/> into a <see cref="CieLuv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLab"/></returns>
        public CieLuv ToCieLuv(CieLchuv color)
        {
            Guard.NotNull(color, nameof(color));

            // Conversion (perserving white point)
            CieLuv unadapted = CieLchuvToCieLuvConverter.Convert(color);

            if (!this.IsChromaticAdaptationPerformed)
            {
                return unadapted;
            }

            // Adaptation
            return this.Adapt(unadapted);
        }

        /// <summary>
        /// Converts a <see cref="CieXyy"/> into a <see cref="CieLuv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLuv"/></returns>
        public CieLuv ToCieLuv(CieXyy color)
        {
            Guard.NotNull(color, nameof(color));

            var xyzColor = this.ToCieXyz(color);
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
            var converter = new CieXyzToCieLuvConverter(this.TargetLuvWhitePoint);
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

            var xyzColor = this.ToCieXyz(color);
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

            var xyzColor = this.ToCieXyz(color);
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

            var xyzColor = this.ToCieXyz(color);
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

            var xyzColor = this.ToCieXyz(color);
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

            var xyzColor = this.ToCieXyz(color);
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

            var xyzColor = this.ToCieXyz(color);
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

            var xyzColor = this.ToCieXyz(color);
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

            var xyzColor = this.ToCieXyz(color);
            return this.ToCieLuv(xyzColor);
        }
    }
}