// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Provides extensions methods for the <see cref="Matrix4x4"/> struct
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public static class Matrix4x4Extensions
    {
        /// <summary>
        /// Create a brightness filter matrix using the given amount.
        /// </summary>
        /// <remarks>
        /// A value of 0 will create an image that is completely black. A value of 1 leaves the input unchanged.
        /// Other values are linear multipliers on the effect. Values of an amount over 1 are allowed, providing brighter results.
        /// </remarks>
        /// <param name="amount">The proportion of the conversion. Must be greater than or equal to 0.</param>
        /// <returns>The <see cref="Matrix4x4"/></returns>
        public static Matrix4x4 CreateBrightnessFilter(float amount)
        {
            Guard.MustBeGreaterThanOrEqualTo(amount, 0, nameof(amount));

            // See https://cs.chromium.org/chromium/src/cc/paint/render_surface_filters.cc
            return new Matrix4x4
            {
                M11 = amount,
                M22 = amount,
                M33 = amount,
                M44 = 1
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
        /// <returns>The <see cref="Matrix4x4"/></returns>
        public static Matrix4x4 CreateContrastFilter(float amount)
        {
            Guard.MustBeGreaterThanOrEqualTo(amount, 0, nameof(amount));

            // See https://cs.chromium.org/chromium/src/cc/paint/render_surface_filters.cc
            float contrast = (-.5F * amount) + .5F;

            return new Matrix4x4
            {
                M11 = amount,
                M22 = amount,
                M33 = amount,
                M41 = contrast,
                M42 = contrast,
                M43 = contrast,
                M44 = 1
            };
        }

        /// <summary>
        /// Create a greyscale filter matrix using the given amount using the formula as specified by ITU-R Recommendation BT.601.
        /// <see href="https://en.wikipedia.org/wiki/Luma_%28video%29#Rec._601_luma_versus_Rec._709_luma_coefficients"/>
        /// </summary>
        /// <param name="amount">The proportion of the conversion. Must be between 0 and 1.</param>
        /// <returns>The <see cref="Matrix4x4"/></returns>
        public static Matrix4x4 CreateGrayscaleBt601Filter(float amount)
        {
            Guard.MustBeBetweenOrEqualTo(amount, 0, 1, nameof(amount));
            amount = 1F - amount;

            // https://cs.chromium.org/chromium/src/cc/paint/render_surface_filters.cc
            return new Matrix4x4
            {
                M11 = .299F + (.701F * amount),
                M12 = .299F - (.299F * amount),
                M13 = .299F - (.299F * amount),
                M21 = .587F - (.587F * amount),
                M22 = .587F + (.413F * amount),
                M23 = .587F - (.587F * amount),
                M31 = .114F - (.114F * amount),
                M32 = .114F - (.114F * amount),
                M33 = .114F + (.886F * amount),
                M44 = 1
            };
        }

        /// <summary>
        /// Create a greyscale filter matrix using the given amount using the formula as specified by ITU-R Recommendation BT.709.
        /// <see href="https://en.wikipedia.org/wiki/Rec._709#Luma_coefficients"/>
        /// </summary>
        /// <param name="amount">The proportion of the conversion. Must be between 0 and 1.</param>
        /// <returns>The <see cref="Matrix4x4"/></returns>
        public static Matrix4x4 CreateGrayscaleBt709Filter(float amount)
        {
            Guard.MustBeBetweenOrEqualTo(amount, 0, 1, nameof(amount));
            amount = 1F - amount;

            // https://cs.chromium.org/chromium/src/cc/paint/render_surface_filters.cc
            return new Matrix4x4
            {
                M11 = .2126F + (.7874F * amount),
                M12 = .2126F - (.2126F * amount),
                M13 = .2126F - (.2126F * amount),
                M21 = .7152F - (.7152F * amount),
                M22 = .7152F + (.2848F * amount),
                M23 = .7152F - (.7152F * amount),
                M31 = .0722F - (.0722F * amount),
                M32 = .0722F - (.0722F * amount),
                M33 = .0722F + (.9278F * amount),
                M44 = 1
            };
        }

        /// <summary>
        /// Create a hue filter matrix using the given angle in degrees.
        /// </summary>
        /// <param name="degrees">The angle of rotation in degrees.</param>
        /// <returns>The <see cref="Matrix4x4"/></returns>
        public static Matrix4x4 CreateHueFilter(float degrees)
        {
            // Wrap the angle round at 360.
            degrees = degrees % 360;

            // Make sure it's not negative.
            while (degrees < 0)
            {
                degrees += 360;
            }

            float radian = MathFExtensions.DegreeToRadian(degrees);
            float cosRadian = MathF.Cos(radian);
            float sinRadian = MathF.Sin(radian);

            // The matrix is set up to preserve the luminance of the image.
            // See http://graficaobscura.com/matrix/index.html
            // Number are taken from https://msdn.microsoft.com/en-us/library/jj192162(v=vs.85).aspx
            return new Matrix4x4
            {
                M11 = .213F + (cosRadian * .787F) - (sinRadian * .213F),
                M12 = .213F - (cosRadian * .213F) - (sinRadian * 0.143F),
                M13 = .213F - (cosRadian * .213F) - (sinRadian * .787F),
                M21 = .715F - (cosRadian * .715F) - (sinRadian * .715F),
                M22 = .715F + (cosRadian * .285F) + (sinRadian * 0.140F),
                M23 = .715F - (cosRadian * .715F) + (sinRadian * .715F),
                M31 = .072F - (cosRadian * .072F) + (sinRadian * .928F),
                M32 = .072F - (cosRadian * .072F) - (sinRadian * 0.283F),
                M33 = .072F + (cosRadian * .928F) + (sinRadian * .072F),
                M44 = 1
            };
        }

        /// <summary>
        /// Create an invert filter matrix using the given amount.
        /// </summary>
        /// <param name="amount">The proportion of the conversion. Must be between 0 and 1.</param>
        /// <returns>The <see cref="Matrix4x4"/></returns>
        public static Matrix4x4 CreateInvertFilter(float amount)
        {
            Guard.MustBeBetweenOrEqualTo(amount, 0, 1, nameof(amount));

            // See https://cs.chromium.org/chromium/src/cc/paint/render_surface_filters.cc
            float invert = 1F - (2F * amount);

            return new Matrix4x4
            {
                M11 = invert,
                M22 = invert,
                M33 = invert,
                M41 = amount,
                M42 = amount,
                M43 = amount,
                M44 = 1
            };
        }

        /// <summary>
        /// Create an opacity filter matrix using the given amount.
        /// </summary>
        /// <param name="amount">The proportion of the conversion. Must be between 0 and 1.</param>
        /// <returns>The <see cref="Matrix4x4"/></returns>
        public static Matrix4x4 CreateOpacityFilter(float amount)
        {
            Guard.MustBeBetweenOrEqualTo(amount, 0, 1, nameof(amount));

            // See https://cs.chromium.org/chromium/src/cc/paint/render_surface_filters.cc
            return new Matrix4x4
            {
                M11 = 1,
                M22 = 1,
                M33 = 1,
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
        /// <returns>The <see cref="Matrix4x4"/></returns>
        public static Matrix4x4 CreateSaturateFilter(float amount)
        {
            Guard.MustBeGreaterThanOrEqualTo(amount, 0, nameof(amount));

            // See https://cs.chromium.org/chromium/src/cc/paint/render_surface_filters.cc
            return new Matrix4x4
            {
                M11 = .213F + (.787F * amount),
                M12 = .213F - (.213F * amount),
                M13 = .213F - (.213F * amount),
                M21 = .715F - (.715F * amount),
                M22 = .715F + (.285F * amount),
                M23 = .715F - (.715F * amount),
                M31 = 1F - ((.213F + (.787F * amount)) + (.715F - (.715F * amount))),
                M32 = 1F - ((.213F - (.213F * amount)) + (.715F + (.285F * amount))),
                M33 = 1F - ((.213F - (.213F * amount)) + (.715F - (.715F * amount))),
                M44 = 1
            };
        }

        /// <summary>
        /// Create a sepia filter matrix using the given amount.
        /// The formula used matches the svg specification. <see href="http://www.w3.org/TR/filter-effects/#sepiaEquivalent"/>
        /// </summary>
        /// <param name="amount">The proportion of the conversion. Must be between 0 and 1.</param>
        /// <returns>The <see cref="Matrix4x4"/></returns>
        public static Matrix4x4 CreateSepiaFilter(float amount)
        {
            Guard.MustBeBetweenOrEqualTo(amount, 0, 1, nameof(amount));
            amount = 1F - amount;

            // See https://cs.chromium.org/chromium/src/cc/paint/render_surface_filters.cc
            return new Matrix4x4
            {
                M11 = .393F + (.607F * amount),
                M12 = .349F - (.349F * amount),
                M13 = .272F - (.272F * amount),
                M21 = .769F - (.769F * amount),
                M22 = .686F + (.314F * amount),
                M23 = .534F - (.534F * amount),
                M31 = .189F - (.189F * amount),
                M32 = .168F - (.168F * amount),
                M33 = .131F + (.869F * amount),
                M44 = 1
            };
        }
    }
}