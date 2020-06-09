// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

// Many of these matrices are translated from Chromium project where
// SkScalar[] is memory-mapped to a row-major matrix.
// The following translates to our column-major form:
//
// | 0| 1| 2| 3| 4|   |0|5|10|15|   |M11|M12|M13|M14|
// | 5| 6| 7| 8| 9|   |1|6|11|16|   |M21|M22|M23|M24|
// |10|11|12|13|14| = |2|7|12|17| = |M31|M32|M33|M34|
// |15|16|17|18|19|   |3|8|13|18|   |M41|M42|M43|M44|
//                    |4|9|14|19|   |M51|M52|M53|M54|
namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// A collection of known <see cref="ColorMatrix"/> values for composing filters
    /// </summary>
    public static class KnownFilterMatrices
    {
        /// <summary>
        /// Gets a filter recreating Achromatomaly (Color desensitivity) color blindness
        /// </summary>
        public static ColorMatrix AchromatomalyFilter { get; } = new ColorMatrix
        {
            M11 = .618F,
            M12 = .163F,
            M13 = .163F,
            M21 = .320F,
            M22 = .775F,
            M23 = .320F,
            M31 = .062F,
            M32 = .062F,
            M33 = .516F,
            M44 = 1
        };

        /// <summary>
        /// Gets a filter recreating Achromatopsia (Monochrome) color blindness.
        /// </summary>
        public static ColorMatrix AchromatopsiaFilter { get; } = new ColorMatrix
        {
            M11 = .299F,
            M12 = .299F,
            M13 = .299F,
            M21 = .587F,
            M22 = .587F,
            M23 = .587F,
            M31 = .114F,
            M32 = .114F,
            M33 = .114F,
            M44 = 1F
        };

        /// <summary>
        /// Gets a filter recreating Deuteranomaly (Green-Weak) color blindness.
        /// </summary>
        public static ColorMatrix DeuteranomalyFilter { get; } = new ColorMatrix
        {
            M11 = .8F,
            M12 = .258F,
            M21 = .2F,
            M22 = .742F,
            M23 = .142F,
            M33 = .858F,
            M44 = 1F
        };

        /// <summary>
        /// Gets a filter recreating Deuteranopia (Green-Blind) color blindness.
        /// </summary>
        public static ColorMatrix DeuteranopiaFilter { get; } = new ColorMatrix
        {
            M11 = .625F,
            M12 = .7F,
            M21 = .375F,
            M22 = .3F,
            M23 = .3F,
            M33 = .7F,
            M44 = 1F
        };

        /// <summary>
        /// Gets a filter recreating Protanomaly (Red-Weak) color blindness.
        /// </summary>
        public static ColorMatrix ProtanomalyFilter { get; } = new ColorMatrix
        {
            M11 = .817F,
            M12 = .333F,
            M21 = .183F,
            M22 = .667F,
            M23 = .125F,
            M33 = .875F,
            M44 = 1F
        };

        /// <summary>
        /// Gets a filter recreating Protanopia (Red-Blind) color blindness.
        /// </summary>
        public static ColorMatrix ProtanopiaFilter { get; } = new ColorMatrix
        {
            M11 = .567F,
            M12 = .558F,
            M21 = .433F,
            M22 = .442F,
            M23 = .242F,
            M33 = .758F,
            M44 = 1F
        };

        /// <summary>
        /// Gets a filter recreating Tritanomaly (Blue-Weak) color blindness.
        /// </summary>
        public static ColorMatrix TritanomalyFilter { get; } = new ColorMatrix
        {
            M11 = .967F,
            M21 = .33F,
            M22 = .733F,
            M23 = .183F,
            M32 = .267F,
            M33 = .817F,
            M44 = 1F
        };

        /// <summary>
        /// Gets a filter recreating Tritanopia (Blue-Blind) color blindness.
        /// </summary>
        public static ColorMatrix TritanopiaFilter { get; } = new ColorMatrix
        {
            M11 = .95F,
            M21 = .05F,
            M22 = .433F,
            M23 = .475F,
            M32 = .567F,
            M33 = .525F,
            M44 = 1F
        };

        /// <summary>
        /// Gets an approximated black and white filter
        /// </summary>
        public static ColorMatrix BlackWhiteFilter { get; } = new ColorMatrix
        {
            M11 = 1.5F,
            M12 = 1.5F,
            M13 = 1.5F,
            M21 = 1.5F,
            M22 = 1.5F,
            M23 = 1.5F,
            M31 = 1.5F,
            M32 = 1.5F,
            M33 = 1.5F,
            M44 = 1F,
            M51 = -1F,
            M52 = -1F,
            M53 = -1F,
        };

        /// <summary>
        /// Gets a filter recreating an old Kodachrome camera effect.
        /// </summary>
        public static ColorMatrix KodachromeFilter { get; } = new ColorMatrix
        {
            M11 = .7297023F,
            M22 = .6109577F,
            M33 = .597218F,
            M44 = 1F,
            M51 = .105F,
            M52 = .145F,
            M53 = .155F,
        }

        * CreateSaturateFilter(1.2F) * CreateContrastFilter(1.35F);

        /// <summary>
        /// Gets a filter recreating an old Lomograph camera effect.
        /// </summary>
        public static ColorMatrix LomographFilter { get; } = new ColorMatrix
        {
            M11 = 1.5F,
            M22 = 1.45F,
            M33 = 1.16F,
            M44 = 1F,
            M51 = -.1F,
            M52 = -.02F,
            M53 = -.07F,
        }

        * CreateSaturateFilter(1.1F) * CreateContrastFilter(1.33F);

        /// <summary>
        /// Gets a filter recreating an old Polaroid camera effect.
        /// </summary>
        public static ColorMatrix PolaroidFilter { get; } = new ColorMatrix
        {
            M11 = 1.538F,
            M12 = -.062F,
            M13 = -.262F,
            M21 = -.022F,
            M22 = 1.578F,
            M23 = -.022F,
            M31 = .216F,
            M32 = -.16F,
            M33 = 1.5831F,
            M44 = 1F,
            M51 = .02F,
            M52 = -.05F,
            M53 = -.05F
        };

        /// <summary>
        /// Create a brightness filter matrix using the given amount.
        /// </summary>
        /// <remarks>
        /// A value of 0 will create an image that is completely black. A value of 1 leaves the input unchanged.
        /// Other values are linear multipliers on the effect. Values of an amount over 1 are allowed, providing brighter results.
        /// </remarks>
        /// <param name="amount">The proportion of the conversion. Must be greater than or equal to 0.</param>
        /// <returns>The <see cref="ColorMatrix"/></returns>
        public static ColorMatrix CreateBrightnessFilter(float amount)
        {
            Guard.MustBeGreaterThanOrEqualTo(amount, 0, nameof(amount));

            // See https://cs.chromium.org/chromium/src/cc/paint/render_surface_filters.cc
            return new ColorMatrix
            {
                M11 = amount,
                M22 = amount,
                M33 = amount,
                M44 = 1F
            };
        }

        /// <summary>
        /// Create a contrast filter matrix using the given amount.
        /// </summary>
        /// <remarks>
        /// A value of 0 will create an image that is completely gray. A value of 1 leaves the input unchanged.
        /// Other values are linear multipliers on the effect. Values of an amount over 1 are allowed, providing results with more contrast.
        /// </remarks>
        /// <param name="amount">The proportion of the conversion. Must be greater than or equal to 0.</param>
        /// <returns>The <see cref="ColorMatrix"/></returns>
        public static ColorMatrix CreateContrastFilter(float amount)
        {
            Guard.MustBeGreaterThanOrEqualTo(amount, 0, nameof(amount));

            // See https://cs.chromium.org/chromium/src/cc/paint/render_surface_filters.cc
            float contrast = (-.5F * amount) + .5F;

            return new ColorMatrix
            {
                M11 = amount,
                M22 = amount,
                M33 = amount,
                M44 = 1F,
                M51 = contrast,
                M52 = contrast,
                M53 = contrast
            };
        }

        /// <summary>
        /// Create a grayscale filter matrix using the given amount using the formula as specified by ITU-R Recommendation BT.601.
        /// <see href="https://en.wikipedia.org/wiki/Luma_%28video%29#Rec._601_luma_versus_Rec._709_luma_coefficients"/>
        /// </summary>
        /// <param name="amount">The proportion of the conversion. Must be between 0 and 1.</param>
        /// <returns>The <see cref="ColorMatrix"/></returns>
        public static ColorMatrix CreateGrayscaleBt601Filter(float amount)
        {
            Guard.MustBeBetweenOrEqualTo(amount, 0, 1F, nameof(amount));
            amount = 1F - amount;

            ColorMatrix m = default;
            m.M11 = .299F + (.701F * amount);
            m.M21 = .587F - (.587F * amount);
            m.M31 = 1F - (m.M11 + m.M21);

            m.M12 = .299F - (.299F * amount);
            m.M22 = .587F + (.2848F * amount);
            m.M32 = 1F - (m.M12 + m.M22);

            m.M13 = .299F - (.299F * amount);
            m.M23 = .587F - (.587F * amount);
            m.M33 = 1F - (m.M13 + m.M23);
            m.M44 = 1F;

            return m;
        }

        /// <summary>
        /// Create a grayscale filter matrix using the given amount using the formula as specified by ITU-R Recommendation BT.709.
        /// <see href="https://en.wikipedia.org/wiki/Rec._709#Luma_coefficients"/>
        /// </summary>
        /// <param name="amount">The proportion of the conversion. Must be between 0 and 1.</param>
        /// <returns>The <see cref="ColorMatrix"/></returns>
        public static ColorMatrix CreateGrayscaleBt709Filter(float amount)
        {
            Guard.MustBeBetweenOrEqualTo(amount, 0, 1F, nameof(amount));
            amount = 1F - amount;

            // https://cs.chromium.org/chromium/src/cc/paint/render_surface_filters.cc
            ColorMatrix m = default;
            m.M11 = .2126F + (.7874F * amount);
            m.M21 = .7152F - (.7152F * amount);
            m.M31 = 1F - (m.M11 + m.M21);

            m.M12 = .2126F - (.2126F * amount);
            m.M22 = .7152F + (.2848F * amount);
            m.M32 = 1F - (m.M12 + m.M22);

            m.M13 = .2126F - (.2126F * amount);
            m.M23 = .7152F - (.7152F * amount);
            m.M33 = 1F - (m.M13 + m.M23);
            m.M44 = 1F;

            return m;
        }

        /// <summary>
        /// Create a hue filter matrix using the given angle in degrees.
        /// </summary>
        /// <param name="degrees">The angle of rotation in degrees.</param>
        /// <returns>The <see cref="ColorMatrix"/></returns>
        public static ColorMatrix CreateHueFilter(float degrees)
        {
            // Wrap the angle round at 360.
            degrees %= 360;

            // Make sure it's not negative.
            while (degrees < 0)
            {
                degrees += 360;
            }

            float radian = GeometryUtilities.DegreeToRadian(degrees);
            float cosRadian = MathF.Cos(radian);
            float sinRadian = MathF.Sin(radian);

            // The matrix is set up to preserve the luminance of the image.
            // See http://graficaobscura.com/matrix/index.html
            // Number are taken from https://msdn.microsoft.com/en-us/library/jj192162(v=vs.85).aspx
            return new ColorMatrix
            {
                M11 = .213F + (cosRadian * .787F) - (sinRadian * .213F),
                M21 = .715F - (cosRadian * .715F) - (sinRadian * .715F),
                M31 = .072F - (cosRadian * .072F) + (sinRadian * .928F),

                M12 = .213F - (cosRadian * .213F) + (sinRadian * .143F),
                M22 = .715F + (cosRadian * .285F) + (sinRadian * .140F),
                M32 = .072F - (cosRadian * .072F) - (sinRadian * .283F),

                M13 = .213F - (cosRadian * .213F) - (sinRadian * .787F),
                M23 = .715F - (cosRadian * .715F) + (sinRadian * .715F),
                M33 = .072F + (cosRadian * .928F) + (sinRadian * .072F),
                M44 = 1F
            };
        }

        /// <summary>
        /// Create an invert filter matrix using the given amount.
        /// </summary>
        /// <param name="amount">The proportion of the conversion. Must be between 0 and 1.</param>
        /// <returns>The <see cref="ColorMatrix"/></returns>
        public static ColorMatrix CreateInvertFilter(float amount)
        {
            Guard.MustBeBetweenOrEqualTo(amount, 0, 1, nameof(amount));

            // See https://cs.chromium.org/chromium/src/cc/paint/render_surface_filters.cc
            float invert = 1F - (2F * amount);

            return new ColorMatrix
            {
                M11 = invert,
                M22 = invert,
                M33 = invert,
                M44 = 1F,
                M51 = amount,
                M52 = amount,
                M53 = amount,
            };
        }

        /// <summary>
        /// Create an opacity filter matrix using the given amount.
        /// </summary>
        /// <param name="amount">The proportion of the conversion. Must be between 0 and 1.</param>
        /// <returns>The <see cref="ColorMatrix"/></returns>
        public static ColorMatrix CreateOpacityFilter(float amount)
        {
            Guard.MustBeBetweenOrEqualTo(amount, 0, 1, nameof(amount));

            // See https://cs.chromium.org/chromium/src/cc/paint/render_surface_filters.cc
            return new ColorMatrix
            {
                M11 = 1F,
                M22 = 1F,
                M33 = 1F,
                M44 = amount
            };
        }

        /// <summary>
        /// Create a saturation filter matrix using the given amount.
        /// </summary>
        /// <remarks>
        /// A value of 0 is completely un-saturated. A value of 1 leaves the input unchanged.
        /// Other values are linear multipliers on the effect. Values of amount over 1 are allowed, providing super-saturated results
        /// </remarks>
        /// <param name="amount">The proportion of the conversion. Must be greater than or equal to 0.</param>
        /// <returns>The <see cref="ColorMatrix"/></returns>
        public static ColorMatrix CreateSaturateFilter(float amount)
        {
            Guard.MustBeGreaterThanOrEqualTo(amount, 0, nameof(amount));

            // See https://cs.chromium.org/chromium/src/cc/paint/render_surface_filters.cc
            ColorMatrix m = default;
            m.M11 = .213F + (.787F * amount);
            m.M21 = .715F - (.715F * amount);
            m.M31 = 1F - (m.M11 + m.M21);

            m.M12 = .213F - (.213F * amount);
            m.M22 = .715F + (.285F * amount);
            m.M32 = 1F - (m.M12 + m.M22);

            m.M13 = .213F - (.213F * amount);
            m.M23 = .715F - (.715F * amount);
            m.M33 = 1F - (m.M13 + m.M23);
            m.M44 = 1F;

            return m;
        }

        /// <summary>
        /// Create a lightness filter matrix using the given amount.
        /// </summary>
        /// <remarks>
        /// A value of 0 will create an image that is completely black. A value of 1 leaves the input unchanged.
        /// Other values are linear multipliers on the effect. Values of an amount over 1 are allowed, providing lighter results.
        /// </remarks>
        /// <param name="amount">The proportion of the conversion. Must be greater than or equal to 0.</param>
        /// <returns>The <see cref="ColorMatrix"/></returns>
        public static ColorMatrix CreateLightnessFilter(float amount)
        {
            Guard.MustBeGreaterThanOrEqualTo(amount, 0, nameof(amount));
            amount--;

            return new ColorMatrix
            {
                M11 = 1F,
                M22 = 1F,
                M33 = 1F,
                M44 = 1F,
                M51 = amount,
                M52 = amount,
                M53 = amount
            };
        }

        /// <summary>
        /// Create a sepia filter matrix using the given amount.
        /// The formula used matches the svg specification. <see href="http://www.w3.org/TR/filter-effects/#sepiaEquivalent"/>
        /// </summary>
        /// <param name="amount">The proportion of the conversion. Must be between 0 and 1.</param>
        /// <returns>The <see cref="ColorMatrix"/></returns>
        public static ColorMatrix CreateSepiaFilter(float amount)
        {
            Guard.MustBeBetweenOrEqualTo(amount, 0, 1, nameof(amount));
            amount = 1F - amount;

            // See https://cs.chromium.org/chromium/src/cc/paint/render_surface_filters.cc
            return new ColorMatrix
            {
                M11 = .393F + (.607F * amount),
                M21 = .769F - (.769F * amount),
                M31 = .189F - (.189F * amount),

                M12 = .349F - (.349F * amount),
                M22 = .686F + (.314F * amount),
                M32 = .168F - (.168F * amount),

                M13 = .272F - (.272F * amount),
                M23 = .534F - (.534F * amount),
                M33 = .131F + (.869F * amount),
                M44 = 1F
            };
        }
    }
}
