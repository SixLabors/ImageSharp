﻿// <copyright file="ColorSpaceConverter.cs" company="James Jackson-South">
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

        /// <summary>
        /// Converts a <see cref="CieLab"/> into a <see cref="Lms"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Lms"/></returns>
        public Lms ToLms(CieLab color)
        {
            Guard.NotNull(color, nameof(color));

            CieXyz xyzColor = this.ToCieXyz(color);
            return this.ToLms(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="HunterLab"/> into a <see cref="Lms"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Lms"/></returns>
        public Lms ToLms(HunterLab color)
        {
            Guard.NotNull(color, nameof(color));

            CieXyz xyzColor = this.ToCieXyz(color);
            return this.ToLms(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="LinearRgb"/> into a <see cref="Lms"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Lms"/></returns>
        public Lms ToLms(LinearRgb color)
        {
            Guard.NotNull(color, nameof(color));

            CieXyz xyzColor = this.ToCieXyz(color);
            return this.ToLms(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="Rgb"/> into a <see cref="Lms"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Lms"/></returns>
        public Lms ToLms(Rgb color)
        {
            Guard.NotNull(color, nameof(color));

            CieXyz xyzColor = this.ToCieXyz(color);
            return this.ToLms(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieLch"/> into a <see cref="Lms"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Lms"/></returns>
        public Lms ToLms(CieLch color)
        {
            Guard.NotNull(color, nameof(color));

            CieXyz xyzColor = this.ToCieXyz(color);
            return this.ToLms(xyzColor);
        }
    }
}