// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion
{
    /// <content>
    /// Performs chromatic adaptation on the various color spaces.
    /// </content>
    internal partial class ColorSpaceConverter
    {
        /// <summary>
        /// Performs chromatic adaptation of given <see cref="CieXyz"/> color.
        /// Target white point is <see cref="WhitePoint"/>.
        /// </summary>
        /// <param name="color">The color to adapt</param>
        /// <param name="sourceWhitePoint">The white point to adapt for</param>
        /// <returns>The adapted color</returns>
        public CieXyz Adapt(in CieXyz color, in CieXyz sourceWhitePoint)
        {
            this.CheckChromaticAdaptation();
            return this.ChromaticAdaptation.Transform(color, sourceWhitePoint, this.WhitePoint);
        }

        /// <summary>
        /// Adapts <see cref="CieLab"/> color from the source white point to white point set in <see cref="TargetLabWhitePoint"/>.
        /// </summary>
        /// <param name="color">The color to adapt</param>
        /// <returns>The adapted color</returns>
        public CieLab Adapt(in CieLab color)
        {
            this.CheckChromaticAdaptation();

            if (color.WhitePoint.Equals(this.TargetLabWhitePoint))
            {
                return color;
            }

            var xyzColor = this.ToCieXyz(color);
            return this.ToCieLab(xyzColor);
        }

        /// <summary>
        /// Adapts <see cref="CieLch"/> color from the source white point to white point set in <see cref="TargetLabWhitePoint"/>.
        /// </summary>
        /// <param name="color">The color to adapt</param>
        /// <returns>The adapted color</returns>
        public CieLch Adapt(in CieLch color)
        {
            this.CheckChromaticAdaptation();

            if (color.WhitePoint.Equals(this.TargetLabWhitePoint))
            {
                return color;
            }

            var labColor = this.ToCieLab(color);
            return this.ToCieLch(labColor);
        }

        /// <summary>
        /// Adapts <see cref="CieLchuv"/> color from the source white point to white point set in <see cref="TargetLabWhitePoint"/>.
        /// </summary>
        /// <param name="color">The color to adapt</param>
        /// <returns>The adapted color</returns>
        public CieLchuv Adapt(in CieLchuv color)
        {
            this.CheckChromaticAdaptation();

            if (color.WhitePoint.Equals(this.TargetLabWhitePoint))
            {
                return color;
            }

            var luvColor = this.ToCieLuv(color);
            return this.ToCieLchuv(luvColor);
        }

        /// <summary>
        /// Adapts <see cref="CieLuv"/> color from the source white point to white point set in <see cref="TargetLuvWhitePoint"/>.
        /// </summary>
        /// <param name="color">The color to adapt</param>
        /// <returns>The adapted color</returns>
        public CieLuv Adapt(in CieLuv color)
        {
            this.CheckChromaticAdaptation();

            if (color.WhitePoint.Equals(this.TargetLuvWhitePoint))
            {
                return color;
            }

            var xyzColor = this.ToCieXyz(color);
            return this.ToCieLuv(xyzColor);
        }

        /// <summary>
        /// Adapts <see cref="HunterLab"/> color from the source white point to white point set in <see cref="TargetHunterLabWhitePoint"/>.
        /// </summary>
        /// <param name="color">The color to adapt</param>
        /// <returns>The adapted color</returns>
        public HunterLab Adapt(in HunterLab color)
        {
            this.CheckChromaticAdaptation();

            if (color.WhitePoint.Equals(this.TargetHunterLabWhitePoint))
            {
                return color;
            }

            var xyzColor = this.ToCieXyz(color);
            return this.ToHunterLab(xyzColor);
        }

        /// <summary>
        /// Adapts a <see cref="LinearRgb"/> color from the source working space to working space set in <see cref="TargetRgbWorkingSpace"/>.
        /// </summary>
        /// <param name="color">The color to adapt</param>
        /// <returns>The adapted color</returns>
        public LinearRgb Adapt(in LinearRgb color)
        {
            this.CheckChromaticAdaptation();

            if (color.WorkingSpace.Equals(this.TargetRgbWorkingSpace))
            {
                return color;
            }

            // Conversion to XYZ
            LinearRgbToCieXyzConverter converterToXYZ = this.GetLinearRgbToCieXyzConverter(color.WorkingSpace);
            CieXyz unadapted = converterToXYZ.Convert(color);

            // Adaptation
            CieXyz adapted = this.ChromaticAdaptation.Transform(unadapted, color.WorkingSpace.WhitePoint, this.TargetRgbWorkingSpace.WhitePoint);

            // Conversion back to RGB
            CieXyzToLinearRgbConverter converterToRGB = this.GetCieXyxToLinearRgbConverter(this.TargetRgbWorkingSpace);
            return converterToRGB.Convert(adapted);
        }

        /// <summary>
        /// Adapts an <see cref="Rgb"/> color from the source working space to working space set in <see cref="TargetRgbWorkingSpace"/>.
        /// </summary>
        /// <param name="color">The color to adapt</param>
        /// <returns>The adapted color</returns>
        public Rgb Adapt(in Rgb color)
        {
            var linearInput = this.ToLinearRgb(color);
            LinearRgb linearOutput = this.Adapt(linearInput);
            return this.ToRgb(linearOutput);
        }

        private void CheckChromaticAdaptation()
        {
            const string NoAdapterMessage = "Cannot perform chromatic adaptation, provide a chromatic adaptation method and white point.";

            if (!this.IsChromaticAdaptationPerformed)
            {
                throw new InvalidOperationException(NoAdapterMessage);
            }
        }
    }
}