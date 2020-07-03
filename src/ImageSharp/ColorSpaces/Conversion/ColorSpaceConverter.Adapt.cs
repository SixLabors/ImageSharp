// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.ColorSpaces.Conversion
{
    /// <content>
    /// Performs chromatic adaptation on the various color spaces.
    /// </content>
    public partial class ColorSpaceConverter
    {
        /// <summary>
        /// Performs chromatic adaptation of given <see cref="CieXyz"/> color.
        /// Target white point is <see cref="ColorSpaceConverterOptions.WhitePoint"/>.
        /// </summary>
        /// <param name="color">The color to adapt</param>
        /// <param name="sourceWhitePoint">The source white point.</param>
        /// <returns>The adapted color</returns>
        public CieXyz Adapt(in CieXyz color, in CieXyz sourceWhitePoint) => this.Adapt(color, sourceWhitePoint, this.whitePoint);

        /// <summary>
        /// Performs chromatic adaptation of given <see cref="CieXyz"/> color.
        /// Target white point is <see cref="ColorSpaceConverterOptions.WhitePoint"/>.
        /// </summary>
        /// <param name="color">The color to adapt</param>
        /// <param name="sourceWhitePoint">The source white point.</param>
        /// <param name="targetWhitePoint">The target white point.</param>
        /// <returns>The adapted color</returns>
        public CieXyz Adapt(in CieXyz color, in CieXyz sourceWhitePoint, in CieXyz targetWhitePoint)
        {
            if (!this.performChromaticAdaptation || sourceWhitePoint.Equals(targetWhitePoint))
            {
                return color;
            }

            return this.chromaticAdaptation.Transform(color, sourceWhitePoint, targetWhitePoint);
        }

        /// <summary>
        /// Adapts <see cref="CieLab"/> color from the source white point to white point set in <see cref="ColorSpaceConverterOptions.TargetLabWhitePoint"/>.
        /// </summary>
        /// <param name="color">The color to adapt</param>
        /// <returns>The adapted color</returns>
        public CieLab Adapt(in CieLab color)
        {
            if (!this.performChromaticAdaptation || color.WhitePoint.Equals(this.targetLabWhitePoint))
            {
                return color;
            }

            var xyzColor = this.ToCieXyz(color);
            return this.ToCieLab(xyzColor);
        }

        /// <summary>
        /// Adapts <see cref="CieLch"/> color from the source white point to white point set in <see cref="ColorSpaceConverterOptions.TargetLabWhitePoint"/>.
        /// </summary>
        /// <param name="color">The color to adapt</param>
        /// <returns>The adapted color</returns>
        public CieLch Adapt(in CieLch color)
        {
            if (!this.performChromaticAdaptation || color.WhitePoint.Equals(this.targetLabWhitePoint))
            {
                return color;
            }

            var labColor = this.ToCieLab(color);
            return this.ToCieLch(labColor);
        }

        /// <summary>
        /// Adapts <see cref="CieLchuv"/> color from the source white point to white point set in <see cref="ColorSpaceConverterOptions.TargetLabWhitePoint"/>.
        /// </summary>
        /// <param name="color">The color to adapt</param>
        /// <returns>The adapted color</returns>
        public CieLchuv Adapt(in CieLchuv color)
        {
            if (!this.performChromaticAdaptation || color.WhitePoint.Equals(this.targetLabWhitePoint))
            {
                return color;
            }

            var luvColor = this.ToCieLuv(color);
            return this.ToCieLchuv(luvColor);
        }

        /// <summary>
        /// Adapts <see cref="CieLuv"/> color from the source white point to white point set in <see cref="ColorSpaceConverterOptions.TargetLuvWhitePoint"/>.
        /// </summary>
        /// <param name="color">The color to adapt</param>
        /// <returns>The adapted color</returns>
        public CieLuv Adapt(in CieLuv color)
        {
            if (!this.performChromaticAdaptation || color.WhitePoint.Equals(this.targetLuvWhitePoint))
            {
                return color;
            }

            var xyzColor = this.ToCieXyz(color);
            return this.ToCieLuv(xyzColor);
        }

        /// <summary>
        /// Adapts <see cref="HunterLab"/> color from the source white point to white point set in <see cref="ColorSpaceConverterOptions.TargetHunterLabWhitePoint"/>.
        /// </summary>
        /// <param name="color">The color to adapt</param>
        /// <returns>The adapted color</returns>
        public HunterLab Adapt(in HunterLab color)
        {
            if (!this.performChromaticAdaptation || color.WhitePoint.Equals(this.targetHunterLabWhitePoint))
            {
                return color;
            }

            var xyzColor = this.ToCieXyz(color);
            return this.ToHunterLab(xyzColor);
        }

        /// <summary>
        /// Adapts a <see cref="LinearRgb"/> color from the source working space to working space set in <see cref="ColorSpaceConverterOptions.TargetRgbWorkingSpace"/>.
        /// </summary>
        /// <param name="color">The color to adapt</param>
        /// <returns>The adapted color</returns>
        public LinearRgb Adapt(in LinearRgb color)
        {
            if (!this.performChromaticAdaptation || color.WorkingSpace.Equals(this.targetRgbWorkingSpace))
            {
                return color;
            }

            // Conversion to XYZ
            LinearRgbToCieXyzConverter converterToXYZ = this.GetLinearRgbToCieXyzConverter(color.WorkingSpace);
            CieXyz unadapted = converterToXYZ.Convert(color);

            // Adaptation
            CieXyz adapted = this.chromaticAdaptation.Transform(unadapted, color.WorkingSpace.WhitePoint, this.targetRgbWorkingSpace.WhitePoint);

            // Conversion back to RGB
            return this.cieXyzToLinearRgbConverter.Convert(adapted);
        }

        /// <summary>
        /// Adapts an <see cref="Rgb"/> color from the source working space to working space set in <see cref="ColorSpaceConverterOptions.TargetRgbWorkingSpace"/>.
        /// </summary>
        /// <param name="color">The color to adapt</param>
        /// <returns>The adapted color</returns>
        public Rgb Adapt(in Rgb color)
        {
            if (!this.performChromaticAdaptation)
            {
                return color;
            }

            var linearInput = this.ToLinearRgb(color);
            LinearRgb linearOutput = this.Adapt(linearInput);
            return this.ToRgb(linearOutput);
        }
    }
}
