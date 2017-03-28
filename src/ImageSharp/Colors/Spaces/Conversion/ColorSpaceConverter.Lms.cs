// <copyright file="ColorSpaceConverter.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Colors.Spaces.Conversion
{
    using ImageSharp.Colors.Spaces;

    /// <summary>
    /// Converts between color spaces ensuring that the color is adapted using chromatic adaptation.
    /// </summary>
    public partial class ColorSpaceConverter
    {
        /// <summary>
        /// Converts a <see cref="CieXyz"/> into a <see cref="Lms"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Lms"/></returns>
        public Lms ToLms(CieXyz color)
        {
            Guard.NotNull(color, nameof(color));

            // Conversion
            return this.cachedCieXyzAndLmsConverter.Convert(color);
        }
    }
}