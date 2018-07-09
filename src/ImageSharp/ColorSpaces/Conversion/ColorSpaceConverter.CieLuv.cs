// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation.CieLchuvColorSapce;
using SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation.CieLuvColorSapce;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion
{
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
        public CieLuv ToCieLuv(in CieLab color)
        {
            var xyzColor = this.ToCieXyz(color);
            return this.ToCieLuv(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieLch"/> into a <see cref="CieLuv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLuv"/></returns>
        public CieLuv ToCieLuv(in CieLch color)
        {
            var xyzColor = this.ToCieXyz(color);
            return this.ToCieLuv(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieLchuv"/> into a <see cref="CieLuv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLab"/></returns>
        public CieLuv ToCieLuv(in CieLchuv color)
        {
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
        public CieLuv ToCieLuv(in CieXyy color)
        {
            var xyzColor = this.ToCieXyz(color);
            return this.ToCieLuv(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieXyz"/> into a <see cref="CieLuv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLuv"/></returns>
        public CieLuv ToCieLuv(in CieXyz color)
        {
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
        public CieLuv ToCieLuv(in Cmyk color)
        {
            var xyzColor = this.ToCieXyz(color);
            return this.ToCieLuv(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="Hsl"/> into a <see cref="CieLuv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLuv"/></returns>
        public CieLuv ToCieLuv(in Hsl color)
        {
            var xyzColor = this.ToCieXyz(color);
            return this.ToCieLuv(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="Hsv"/> into a <see cref="CieLuv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLuv"/></returns>
        public CieLuv ToCieLuv(in Hsv color)
        {
            var xyzColor = this.ToCieXyz(color);
            return this.ToCieLuv(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="HunterLab"/> into a <see cref="CieLuv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLuv"/></returns>
        public CieLuv ToCieLuv(in HunterLab color)
        {
            var xyzColor = this.ToCieXyz(color);
            return this.ToCieLuv(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="Lms"/> into a <see cref="CieLuv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLuv"/></returns>
        public CieLuv ToCieLuv(in Lms color)
        {
            var xyzColor = this.ToCieXyz(color);
            return this.ToCieLuv(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="LinearRgb"/> into a <see cref="CieLuv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLuv"/></returns>
        public CieLuv ToCieLuv(in LinearRgb color)
        {
            var xyzColor = this.ToCieXyz(color);
            return this.ToCieLuv(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="Rgb"/> into a <see cref="CieLuv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLuv"/></returns>
        public CieLuv ToCieLuv(in Rgb color)
        {
            var xyzColor = this.ToCieXyz(color);
            return this.ToCieLuv(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="YCbCr"/> into a <see cref="CieLuv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLuv"/></returns>
        public CieLuv ToCieLuv(in YCbCr color)
        {
            var xyzColor = this.ToCieXyz(color);
            return this.ToCieLuv(xyzColor);
        }
    }
}