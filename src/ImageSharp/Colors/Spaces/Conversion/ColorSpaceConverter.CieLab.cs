// <copyright file="ColorSpaceConverter.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Colors.Spaces.Conversion
{
    using ImageSharp.Colors.Spaces;
    using ImageSharp.Colors.Spaces.Conversion.Implementation.CieLab;
    using ImageSharp.Colors.Spaces.Conversion.Implementation.CieLch;

    /// <summary>
    /// Converts between color spaces ensuring that the color is adapted using chromatic adaptation.
    /// </summary>
    public partial class ColorSpaceConverter
    {
        /// <summary>
        /// The converter for converting between CieLch to CieLab.
        /// </summary>
        private static readonly CieLchToCieLabConverter CieLchToCieLabConverter = new CieLchToCieLabConverter();

        /// <summary>
        /// Converts a <see cref="CieXyz"/> into a <see cref="CieLab"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLab"/></returns>
        public CieLab ToCieLab(CieXyz color)
        {
            Guard.NotNull(color, nameof(color));

            // Adaptation
            CieXyz adapted = !this.WhitePoint.Equals(this.TargetLabWhitePoint) && this.IsChromaticAdaptationPerformed
                ? this.ChromaticAdaptation.Transform(color, this.WhitePoint, this.TargetLabWhitePoint)
                : color;

            // Conversion
            CieXyzToCieLabConverter converter = new CieXyzToCieLabConverter(this.TargetLabWhitePoint);
            return converter.Convert(adapted);
        }

        /// <summary>
        /// Converts a <see cref="LinearRgb"/> into a <see cref="CieLab"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLab"/></returns>
        public CieLab ToCieLab(LinearRgb color)
        {
            Guard.NotNull(color, nameof(color));

            CieXyz xyzColor = this.ToCieXyz(color);
            return this.ToCieLab(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="Rgb"/> into a <see cref="CieLab"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLab"/></returns>
        public CieLab ToCieLab(Rgb color)
        {
            Guard.NotNull(color, nameof(color));

            CieXyz xyzColor = this.ToCieXyz(color);
            return this.ToCieLab(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="HunterLab"/> into a <see cref="CieLab"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLab"/></returns>
        public CieLab ToCieLab(HunterLab color)
        {
            Guard.NotNull(color, nameof(color));

            CieXyz xyzColor = this.ToCieXyz(color);
            return this.ToCieLab(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="Lms"/> into a <see cref="CieLab"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLab"/></returns>
        public CieLab ToCieLab(Lms color)
        {
            Guard.NotNull(color, nameof(color));

            CieXyz xyzColor = this.ToCieXyz(color);
            return this.ToCieLab(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieLch"/> into a <see cref="CieLab"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLab"/></returns>
        public CieLab ToCieLab(CieLch color)
        {
            Guard.NotNull(color, nameof(color));

            // Conversion (perserving white point)
            CieLab unadapted = CieLchToCieLabConverter.Convert(color);

            if (!this.IsChromaticAdaptationPerformed)
            {
                return unadapted;
            }

            // Adaptation
            return this.Adapt(unadapted);
        }
    }
}