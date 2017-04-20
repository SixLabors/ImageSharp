// <copyright file="ColorSpaceConverter.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Colors.Spaces.Conversion
{
    using ImageSharp.Colors.Spaces;
    using ImageSharp.Colors.Spaces.Conversion.Implementation.CieLab;
    using ImageSharp.Colors.Spaces.Conversion.Implementation.HunterLab;
    using ImageSharp.Colors.Spaces.Conversion.Implementation.Rgb;

    /// <summary>
    /// Converts between color spaces ensuring that the color is adapted using chromatic adaptation.
    /// </summary>
    public partial class ColorSpaceConverter
    {
        private static readonly CieLabToCieXyzConverter CieLabToCieXyzConverter = new CieLabToCieXyzConverter();

        private static readonly HunterLabToCieXyzConverter HunterLabToCieXyzConverter = new HunterLabToCieXyzConverter();

        private LinearRgbToCieXyzConverter linearRgbToCieXyzConverter;

        /// <summary>
        /// Converts a <see cref="CieLab"/> into a <see cref="CieXyz"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieXyz"/></returns>
        public CieXyz ToCieXyz(CieLab color)
        {
            Guard.NotNull(color, nameof(color));

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
        public CieXyz ToCieXyz(CieLch color)
        {
            Guard.NotNull(color, nameof(color));

            // Conversion to Lab
            CieLab labColor = CieLchToCieLabConverter.Convert(color);

            // Conversion to XYZ (incl. adaptation)
            return this.ToCieXyz(labColor);
        }

        /// <summary>
        /// Converts a <see cref="CieXyy"/> into a <see cref="CieXyz"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieXyz"/></returns>
        public CieXyz ToCieXyz(CieXyy color)
        {
            Guard.NotNull(color, nameof(color));

            // Conversion
            return CieXyzAndCieXyyConverter.Convert(color);
        }

        /// <summary>
        /// Converts a <see cref="Cmyk"/> into a <see cref="CieXyz"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieXyz"/></returns>
        public CieXyz ToCieXyz(Cmyk color)
        {
            Guard.NotNull(color, nameof(color));

            // Conversion
            Rgb rgb = this.ToRgb(color);

            return this.ToCieXyz(rgb);
        }

        /// <summary>
        /// Converts a <see cref="HunterLab"/> into a <see cref="CieXyz"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieXyz"/></returns>
        public CieXyz ToCieXyz(HunterLab color)
        {
            Guard.NotNull(color, nameof(color));

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
        public CieXyz ToCieXyz(LinearRgb color)
        {
            Guard.NotNull(color, nameof(color));

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
        public CieXyz ToCieXyz(Lms color)
        {
            Guard.NotNull(color, nameof(color));

            // Conversion
            return this.cachedCieXyzAndLmsConverter.Convert(color);
        }

        /// <summary>
        /// Converts a <see cref="Rgb"/> into a <see cref="CieXyz"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieXyz"/></returns>
        public CieXyz ToCieXyz(Rgb color)
        {
            Guard.NotNull(color, nameof(color));

            // Conversion
            LinearRgb linear = RgbToLinearRgbConverter.Convert(color);
            return this.ToCieXyz(linear);
        }

        /// <summary>
        /// Gets the correct converter for the given rgb working space.
        /// </summary>
        /// <param name="workingSpace">The source working space</param>
        /// <returns>The <see cref="LinearRgbToCieXyzConverter"/></returns>
        private LinearRgbToCieXyzConverter GetLinearRgbToCieXyzConverter(IRgbWorkingSpace workingSpace)
        {
            if (this.linearRgbToCieXyzConverter != null && this.linearRgbToCieXyzConverter.SourceWorkingSpace.Equals(workingSpace))
            {
                return this.linearRgbToCieXyzConverter;
            }

            return this.linearRgbToCieXyzConverter = new LinearRgbToCieXyzConverter(workingSpace);
        }
    }
}