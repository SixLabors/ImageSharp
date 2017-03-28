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
        private static readonly LinearRgbToRgbConverter LinearRgbToRgbConverter = new LinearRgbToRgbConverter();

        /// <summary>
        /// Converts a <see cref="LinearRgb"/> into a <see cref="Rgb"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Rgb"/></returns>
        public Rgb ToRgb(LinearRgb color)
        {
            Guard.NotNull(color, nameof(color));

            // Conversion
            return LinearRgbToRgbConverter.Convert(color);
        }

        /// <summary>
        /// Converts a <see cref="CieXyz"/> into a <see cref="Rgb"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Rgb"/></returns>
        public Rgb ToRgb(CieXyz color)
        {
            Guard.NotNull(color, nameof(color));

            // Conversion
            LinearRgb linear = this.ToLinearRgb(color);

            // Compand
            return this.ToRgb(linear);
        }
    }
}