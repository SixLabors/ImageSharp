// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation.CieLabColorSapce;
using SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation.CieLuvColorSapce;
using SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation.HunterLabColorSapce;
using SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation.RgbColorSapce;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion
{
    /// <content>
    /// Allows conversion to <see cref="CieXyz"/>.
    /// </content>
    internal partial class ColorSpaceConverter
    {
        private static readonly CieLabToCieXyzConverter CieLabToCieXyzConverter = new CieLabToCieXyzConverter();

        private static readonly CieLuvToCieXyzConverter CieLuvToCieXyzConverter = new CieLuvToCieXyzConverter();

        private static readonly HunterLabToCieXyzConverter HunterLabToCieXyzConverter = new HunterLabToCieXyzConverter();

        private LinearRgbToCieXyzConverter linearRgbToCieXyzConverter;

        /// <summary>
        /// Converts a <see cref="CieLab"/> into a <see cref="CieXyz"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieXyz"/></returns>
        public CieXyz ToCieXyz(in CieLab color)
        {
            // Conversion
            CieXyz unadapted = CieLabToCieXyzConverter.Convert(color);

            // Adaptation
            CieXyz adapted = color.WhitePoint.Equals(this.WhitePoint) || !this.IsChromaticAdaptationPerformed
                ? unadapted
                : this.Adapt(unadapted, color.WhitePoint);

            return adapted;
        }

        /// <summary>
        /// Converts a <see cref="CieLch"/> into a <see cref="CieXyz"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieXyz"/></returns>
        public CieXyz ToCieXyz(in CieLch color)
        {
            // Conversion to Lab
            CieLab labColor = CieLchToCieLabConverter.Convert(color);

            // Conversion to XYZ (incl. adaptation)
            return this.ToCieXyz(labColor);
        }

        /// <summary>
        /// Converts a <see cref="CieLuv"/> into a <see cref="CieXyz"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieXyz"/></returns>
        public CieXyz ToCieXyz(in CieLchuv color)
        {
            // Conversion to Luv
            CieLuv luvColor = CieLchuvToCieLuvConverter.Convert(color);

            // Conversion to XYZ (incl. adaptation)
            return this.ToCieXyz(luvColor);
        }

        /// <summary>
        /// Converts a <see cref="CieLuv"/> into a <see cref="CieXyz"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieXyz"/></returns>
        public CieXyz ToCieXyz(in CieLuv color)
        {
            // Conversion
            CieXyz unadapted = CieLuvToCieXyzConverter.Convert(color);

            // Adaptation
            CieXyz adapted = color.WhitePoint.Equals(this.WhitePoint) || !this.IsChromaticAdaptationPerformed
                              ? unadapted
                              : this.Adapt(unadapted, color.WhitePoint);

            return adapted;
        }

        /// <summary>
        /// Converts a <see cref="CieXyy"/> into a <see cref="CieXyz"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieXyz"/></returns>
        public CieXyz ToCieXyz(in CieXyy color)
        {
            // Conversion
            return CieXyzAndCieXyyConverter.Convert(color);
        }

        /// <summary>
        /// Converts a <see cref="Cmyk"/> into a <see cref="CieXyz"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieXyz"/></returns>
        public CieXyz ToCieXyz(in Cmyk color)
        {
            // Conversion
            var rgb = this.ToRgb(color);

            return this.ToCieXyz(rgb);
        }

        /// <summary>
        /// Converts a <see cref="Hsl"/> into a <see cref="CieXyz"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieXyz"/></returns>
        public CieXyz ToCieXyz(in Hsl color)
        {
            // Conversion
            var rgb = this.ToRgb(color);

            return this.ToCieXyz(rgb);
        }

        /// <summary>
        /// Converts a <see cref="Hsv"/> into a <see cref="CieXyz"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieXyz"/></returns>
        public CieXyz ToCieXyz(in Hsv color)
        {
            // Conversion
            var rgb = this.ToRgb(color);

            return this.ToCieXyz(rgb);
        }

        /// <summary>
        /// Converts a <see cref="HunterLab"/> into a <see cref="CieXyz"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieXyz"/></returns>
        public CieXyz ToCieXyz(in HunterLab color)
        {
            // Conversion
            CieXyz unadapted = HunterLabToCieXyzConverter.Convert(color);

            // Adaptation
            CieXyz adapted = color.WhitePoint.Equals(this.WhitePoint) || !this.IsChromaticAdaptationPerformed
                                 ? unadapted
                                 : this.Adapt(unadapted, color.WhitePoint);

            return adapted;
        }

        /// <summary>
        /// Converts a <see cref="LinearRgb"/> into a <see cref="CieXyz"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieXyz"/></returns>
        public CieXyz ToCieXyz(in LinearRgb color)
        {
            // Conversion
            LinearRgbToCieXyzConverter converter = this.GetLinearRgbToCieXyzConverter(color.WorkingSpace);
            CieXyz unadapted = converter.Convert(color);

            // Adaptation
            return color.WorkingSpace.WhitePoint.Equals(this.WhitePoint) || !this.IsChromaticAdaptationPerformed
                       ? unadapted
                       : this.Adapt(unadapted, color.WorkingSpace.WhitePoint);
        }

        /// <summary>
        /// Converts a <see cref="Lms"/> into a <see cref="CieXyz"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieXyz"/></returns>
        public CieXyz ToCieXyz(in Lms color)
        {
            // Conversion
            return this.cachedCieXyzAndLmsConverter.Convert(color);
        }

        /// <summary>
        /// Converts a <see cref="Rgb"/> into a <see cref="CieXyz"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieXyz"/></returns>
        public CieXyz ToCieXyz(in Rgb color)
        {
            // Conversion
            LinearRgb linear = RgbToLinearRgbConverter.Convert(color);
            return this.ToCieXyz(linear);
        }

        /// <summary>
        /// Converts a <see cref="YCbCr"/> into a <see cref="CieXyz"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieXyz"/></returns>
        public CieXyz ToCieXyz(in YCbCr color)
        {
            // Conversion
            var rgb = this.ToRgb(color);

            return this.ToCieXyz(rgb);
        }

        /// <summary>
        /// Gets the correct converter for the given rgb working space.
        /// </summary>
        /// <param name="workingSpace">The source working space</param>
        /// <returns>The <see cref="LinearRgbToCieXyzConverter"/></returns>
        private LinearRgbToCieXyzConverter GetLinearRgbToCieXyzConverter(RgbWorkingSpace workingSpace)
        {
            if (this.linearRgbToCieXyzConverter != null && this.linearRgbToCieXyzConverter.SourceWorkingSpace.Equals(workingSpace))
            {
                return this.linearRgbToCieXyzConverter;
            }

            return this.linearRgbToCieXyzConverter = new LinearRgbToCieXyzConverter(workingSpace);
        }
    }
}