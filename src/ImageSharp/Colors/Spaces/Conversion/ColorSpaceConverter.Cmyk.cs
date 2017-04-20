// <copyright file="ColorSpaceConverter.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Colors.Spaces.Conversion
{
    using ImageSharp.Colors.Spaces;
    using ImageSharp.Colors.Spaces.Conversion.Implementation.Cmyk;

    /// <summary>
    /// Converts between color spaces ensuring that the color is adapted using chromatic adaptation.
    /// </summary>
    public partial class ColorSpaceConverter
    {
        private static readonly CmykAndRgbConverter CmykAndRgbConverter = new CmykAndRgbConverter();

        /// <summary>
        /// Converts a <see cref="CieLab"/> into a <see cref="Cmyk"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Cmyk"/></returns>
        public Cmyk ToCmyk(CieLab color)
        {
            Guard.NotNull(color, nameof(color));

            CieXyz xyzColor = this.ToCieXyz(color);

            return this.ToCmyk(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieLch"/> into a <see cref="Cmyk"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Cmyk"/></returns>
        public Cmyk ToCmyk(CieLch color)
        {
            Guard.NotNull(color, nameof(color));

            CieXyz xyzColor = this.ToCieXyz(color);

            return this.ToCmyk(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieXyy"/> into a <see cref="Cmyk"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Cmyk"/></returns>
        public Cmyk ToCmyk(CieXyy color)
        {
            Guard.NotNull(color, nameof(color));

            CieXyz xyzColor = this.ToCieXyz(color);

            return this.ToCmyk(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieXyz"/> into a <see cref="Cmyk"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Cmyk"/></returns>
        public Cmyk ToCmyk(CieXyz color)
        {
            Guard.NotNull(color, nameof(color));

            Rgb rgb = this.ToRgb(color);

            return CmykAndRgbConverter.Convert(rgb);
        }

        /// <summary>
        /// Converts a <see cref="HunterLab"/> into a <see cref="Cmyk"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Cmyk"/></returns>
        public Cmyk ToCmyk(HunterLab color)
        {
            Guard.NotNull(color, nameof(color));

            CieXyz xyzColor = this.ToCieXyz(color);

            return this.ToCmyk(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="LinearRgb"/> into a <see cref="Cmyk"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Cmyk"/></returns>
        public Cmyk ToCmyk(LinearRgb color)
        {
            Guard.NotNull(color, nameof(color));

            Rgb rgb = this.ToRgb(color);

            return CmykAndRgbConverter.Convert(rgb);
        }

        /// <summary>
        /// Converts a <see cref="Lms"/> into a <see cref="Cmyk"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Cmyk"/></returns>
        public Cmyk ToCmyk(Lms color)
        {
            Guard.NotNull(color, nameof(color));

            CieXyz xyzColor = this.ToCieXyz(color);

            return this.ToCmyk(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="Rgb"/> into a <see cref="Cmyk"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Cmyk"/></returns>
        public Cmyk ToCmyk(Rgb color)
        {
            Guard.NotNull(color, nameof(color));

            return CmykAndRgbConverter.Convert(color);
        }
    }
}