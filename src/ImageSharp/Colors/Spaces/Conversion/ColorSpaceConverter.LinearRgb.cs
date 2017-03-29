// <copyright file="ColorSpaceConverter.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Colors.Spaces.Conversion
{
    using ImageSharp.Colors.Spaces.Conversion.Implementation.Rgb;

    /// <summary>
    /// Converts between color spaces ensuring that the color is adapted using chromatic adaptation.
    /// </summary>
    public partial class ColorSpaceConverter
    {
        private static readonly RgbToLinearRgbConverter RgbToLinearRgbConverter = new RgbToLinearRgbConverter();

        private CieXyzToLinearRgbConverter cieXyzToLinearRgbConverter;

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
        /// Converts a <see cref="HunterLab"/> into a <see cref="LinearRgb"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="LinearRgb"/></returns>
        public LinearRgb ToLinearRGB(HunterLab color)
        {
            Guard.NotNull(color, nameof(color));

            CieXyz xyzColor = this.ToCieXyz(color);
            return this.ToLinearRgb(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieLab"/> into a <see cref="LinearRgb"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="LinearRgb"/></returns>
        public LinearRgb ToLinearRGB(CieLab color)
        {
            Guard.NotNull(color, nameof(color));

            CieXyz xyzColor = this.ToCieXyz(color);
            return this.ToLinearRgb(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="Lms"/> into a <see cref="LinearRgb"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="LinearRgb"/></returns>
        public LinearRgb ToLinearRGB(Lms color)
        {
            Guard.NotNull(color, nameof(color));

            CieXyz xyzColor = this.ToCieXyz(color);
            return this.ToLinearRgb(xyzColor);
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