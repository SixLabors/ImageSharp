// <copyright file="ColorSpaceConverter.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Colors.Spaces.Conversion
{
    using ImageSharp.Colors.Spaces;
    using ImageSharp.Colors.Spaces.Conversion.Implementation.CieLab;

    /// <summary>
    /// Converts between color spaces ensuring that the color is adapted using chromatic adaptation.
    /// </summary>
    public partial class ColorSpaceConverter
    {
        private static readonly CieLabToCieXyzConverter CieLabToCieXyzConverter = new CieLabToCieXyzConverter();

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
    }
}