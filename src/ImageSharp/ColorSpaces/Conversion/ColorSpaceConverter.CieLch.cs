// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation.CieLchColorSapce;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion
{
    /// <content>
    /// Allows conversion to <see cref="CieLch"/>.
    /// </content>
    internal partial class ColorSpaceConverter
    {
        /// <summary>
        /// The converter for converting between CieLab to CieLch.
        /// </summary>
        private static readonly CieLabToCieLchConverter CieLabToCieLchConverter = new CieLabToCieLchConverter();

        /// <summary>
        /// Converts a <see cref="CieLab"/> into a <see cref="CieLch"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLch"/></returns>
        public CieLch ToCieLch(in CieLab color)
        {
            // Adaptation
            CieLab adapted = this.IsChromaticAdaptationPerformed ? this.Adapt(color) : color;

            // Conversion
            return CieLabToCieLchConverter.Convert(adapted);
        }

        /// <summary>
        /// Converts a <see cref="CieLchuv"/> into a <see cref="CieLch"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLch"/></returns>
        public CieLch ToCieLch(in CieLchuv color)
        {
            CieXyz xyzColor = this.ToCieXyz(color);
            return this.ToCieLch(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieLuv"/> into a <see cref="CieLch"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLch"/></returns>
        public CieLch ToCieLch(in CieLuv color)
        {
            CieXyz xyzColor = this.ToCieXyz(color);
            return this.ToCieLch(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieXyy"/> into a <see cref="CieLch"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLch"/></returns>
        public CieLch ToCieLch(in CieXyy color)
        {
            CieXyz xyzColor = this.ToCieXyz(color);
            return this.ToCieLch(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieXyz"/> into a <see cref="CieLch"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLch"/></returns>
        public CieLch ToCieLch(in CieXyz color)
        {
            CieLab labColor = this.ToCieLab(color);
            return this.ToCieLch(labColor);
        }

        /// <summary>
        /// Converts a <see cref="Cmyk"/> into a <see cref="CieLch"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLch"/></returns>
        public CieLch ToCieLch(in Cmyk color)
        {
            CieXyz xyzColor = this.ToCieXyz(color);
            return this.ToCieLch(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="Hsl"/> into a <see cref="CieLch"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLch"/></returns>
        public CieLch ToCieLch(in Hsl color)
        {
            CieXyz xyzColor = this.ToCieXyz(color);
            return this.ToCieLch(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="Hsv"/> into a <see cref="CieLch"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLch"/></returns>
        public CieLch ToCieLch(in Hsv color)
        {
            CieXyz xyzColor = this.ToCieXyz(color);
            return this.ToCieLch(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="HunterLab"/> into a <see cref="CieLch"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLch"/></returns>
        public CieLch ToCieLch(in HunterLab color)
        {
            CieXyz xyzColor = this.ToCieXyz(color);
            return this.ToCieLch(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="LinearRgb"/> into a <see cref="CieLch"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLch"/></returns>
        public CieLch ToCieLch(in LinearRgb color)
        {
            CieXyz xyzColor = this.ToCieXyz(color);
            return this.ToCieLch(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="Lms"/> into a <see cref="CieLch"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLch"/></returns>
        public CieLch ToCieLch(in Lms color)
        {
            CieXyz xyzColor = this.ToCieXyz(color);
            return this.ToCieLch(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="Rgb"/> into a <see cref="CieLch"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLch"/></returns>
        public CieLch ToCieLch(in Rgb color)
        {
            CieXyz xyzColor = this.ToCieXyz(color);
            return this.ToCieLch(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="YCbCr"/> into a <see cref="CieLch"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLch"/></returns>
        public CieLch ToCieLch(in YCbCr color)
        {
            CieXyz xyzColor = this.ToCieXyz(color);
            return this.ToCieLch(xyzColor);
        }
    }
}