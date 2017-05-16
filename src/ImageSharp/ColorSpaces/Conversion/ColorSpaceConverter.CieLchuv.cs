// <copyright file="ColorSpaceConverter.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.ColorSpaces.Conversion
{
    using ImageSharp.ColorSpaces.Conversion.Implementation.CieLchuv;

    /// <content>
    /// Allows conversion to <see cref="CieLchuv"/>.
    /// </content>
    internal partial class ColorSpaceConverter
    {
        /// <summary>
        /// The converter for converting between CieLab to CieLchuv.
        /// </summary>
        private static readonly CieLuvToCieLchuvConverter CieLuvToCieLchuvConverter = new CieLuvToCieLchuvConverter();

        /// <summary>
        /// Converts a <see cref="CieLab"/> into a <see cref="CieLchuv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLchuv"/></returns>
        public CieLchuv ToCieLchuv(CieLab color)
        {
            Guard.NotNull(color, nameof(color));

            CieXyz xyzColor = this.ToCieXyz(color);
            return this.ToCieLchuv(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieLch"/> into a <see cref="CieLchuv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLchuv"/></returns>
        public CieLchuv ToCieLchuv(CieLch color)
        {
            Guard.NotNull(color, nameof(color));

            CieXyz xyzColor = this.ToCieXyz(color);
            return this.ToCieLchuv(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieLuv"/> into a <see cref="CieLchuv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLchuv"/></returns>
        public CieLchuv ToCieLchuv(CieLuv color)
        {
            Guard.NotNull(color, nameof(color));

            // Adaptation
            CieLuv adapted = this.IsChromaticAdaptationPerformed ? this.Adapt(color) : color;

            // Conversion
            return CieLuvToCieLchuvConverter.Convert(adapted);
        }

        /// <summary>
        /// Converts a <see cref="CieXyy"/> into a <see cref="CieLchuv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLchuv"/></returns>
        public CieLchuv ToCieLchuv(CieXyy color)
        {
            Guard.NotNull(color, nameof(color));

            CieXyz xyzColor = this.ToCieXyz(color);
            return this.ToCieLchuv(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieXyz"/> into a <see cref="CieLchuv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLchuv"/></returns>
        public CieLchuv ToCieLchuv(CieXyz color)
        {
            Guard.NotNull(color, nameof(color));

            CieLab labColor = this.ToCieLab(color);
            return this.ToCieLchuv(labColor);
        }

        /// <summary>
        /// Converts a <see cref="Cmyk"/> into a <see cref="CieLchuv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLchuv"/></returns>
        public CieLchuv ToCieLchuv(Cmyk color)
        {
            Guard.NotNull(color, nameof(color));

            CieXyz xyzColor = this.ToCieXyz(color);
            return this.ToCieLchuv(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="Hsl"/> into a <see cref="CieLchuv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLchuv"/></returns>
        public CieLchuv ToCieLchuv(Hsl color)
        {
            Guard.NotNull(color, nameof(color));

            CieXyz xyzColor = this.ToCieXyz(color);
            return this.ToCieLchuv(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="Hsv"/> into a <see cref="CieLchuv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLchuv"/></returns>
        public CieLchuv ToCieLchuv(Hsv color)
        {
            Guard.NotNull(color, nameof(color));

            CieXyz xyzColor = this.ToCieXyz(color);
            return this.ToCieLchuv(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="HunterLab"/> into a <see cref="CieLchuv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLchuv"/></returns>
        public CieLchuv ToCieLchuv(HunterLab color)
        {
            Guard.NotNull(color, nameof(color));

            CieXyz xyzColor = this.ToCieXyz(color);
            return this.ToCieLchuv(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="LinearRgb"/> into a <see cref="CieLchuv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLchuv"/></returns>
        public CieLchuv ToCieLchuv(LinearRgb color)
        {
            Guard.NotNull(color, nameof(color));

            CieXyz xyzColor = this.ToCieXyz(color);
            return this.ToCieLchuv(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="Lms"/> into a <see cref="CieLchuv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLchuv"/></returns>
        public CieLchuv ToCieLchuv(Lms color)
        {
            Guard.NotNull(color, nameof(color));

            CieXyz xyzColor = this.ToCieXyz(color);
            return this.ToCieLchuv(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="Rgb"/> into a <see cref="CieLchuv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLchuv"/></returns>
        public CieLchuv ToCieLchuv(Rgb color)
        {
            Guard.NotNull(color, nameof(color));

            CieXyz xyzColor = this.ToCieXyz(color);
            return this.ToCieLchuv(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="YCbCr"/> into a <see cref="CieLchuv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLchuv"/></returns>
        public CieLchuv ToCieLchuv(YCbCr color)
        {
            Guard.NotNull(color, nameof(color));

            CieXyz xyzColor = this.ToCieXyz(color);
            return this.ToCieLchuv(xyzColor);
        }
    }
}