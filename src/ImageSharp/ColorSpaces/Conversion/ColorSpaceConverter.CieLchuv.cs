// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation.CieLchuvColorSapce;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion
{
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
        public CieLchuv ToCieLchuv(in CieLab color)
        {
            CieXyz xyzColor = this.ToCieXyz(color);
            return this.ToCieLchuv(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieLch"/> into a <see cref="CieLchuv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLchuv"/></returns>
        public CieLchuv ToCieLchuv(in CieLch color)
        {
            CieXyz xyzColor = this.ToCieXyz(color);
            return this.ToCieLchuv(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieLuv"/> into a <see cref="CieLchuv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLchuv"/></returns>
        public CieLchuv ToCieLchuv(in CieLuv color)
        {
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
        public CieLchuv ToCieLchuv(in CieXyy color)
        {
            CieXyz xyzColor = this.ToCieXyz(color);
            return this.ToCieLchuv(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieXyz"/> into a <see cref="CieLchuv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLchuv"/></returns>
        public CieLchuv ToCieLchuv(in CieXyz color)
        {
            CieLab labColor = this.ToCieLab(color);
            return this.ToCieLchuv(labColor);
        }

        /// <summary>
        /// Converts a <see cref="Cmyk"/> into a <see cref="CieLchuv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLchuv"/></returns>
        public CieLchuv ToCieLchuv(in Cmyk color)
        {
            CieXyz xyzColor = this.ToCieXyz(color);
            return this.ToCieLchuv(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="Hsl"/> into a <see cref="CieLchuv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLchuv"/></returns>
        public CieLchuv ToCieLchuv(in Hsl color)
        {
            CieXyz xyzColor = this.ToCieXyz(color);
            return this.ToCieLchuv(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="Hsv"/> into a <see cref="CieLchuv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLchuv"/></returns>
        public CieLchuv ToCieLchuv(in Hsv color)
        {
            CieXyz xyzColor = this.ToCieXyz(color);
            return this.ToCieLchuv(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="HunterLab"/> into a <see cref="CieLchuv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLchuv"/></returns>
        public CieLchuv ToCieLchuv(in HunterLab color)
        {
            CieXyz xyzColor = this.ToCieXyz(color);
            return this.ToCieLchuv(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="LinearRgb"/> into a <see cref="CieLchuv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLchuv"/></returns>
        public CieLchuv ToCieLchuv(in LinearRgb color)
        {
            CieXyz xyzColor = this.ToCieXyz(color);
            return this.ToCieLchuv(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="Lms"/> into a <see cref="CieLchuv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLchuv"/></returns>
        public CieLchuv ToCieLchuv(in Lms color)
        {
            CieXyz xyzColor = this.ToCieXyz(color);
            return this.ToCieLchuv(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="Rgb"/> into a <see cref="CieLchuv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLchuv"/></returns>
        public CieLchuv ToCieLchuv(in Rgb color)
        {
            CieXyz xyzColor = this.ToCieXyz(color);
            return this.ToCieLchuv(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="YCbCr"/> into a <see cref="CieLchuv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLchuv"/></returns>
        public CieLchuv ToCieLchuv(in YCbCr color)
        {
            CieXyz xyzColor = this.ToCieXyz(color);
            return this.ToCieLchuv(xyzColor);
        }
    }
}