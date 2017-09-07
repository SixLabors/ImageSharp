// <copyright file="BmpColorSpaceIntent.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>
namespace ImageSharp.Formats
{
    /// <summary>
    /// The rendering intent used on the Microsoft Windows BMP v5 (and later versions) image DIB or file.
    /// </summary>
    /// See <a href="https://msdn.microsoft.com/en-us/library/dd183381(v=vs.85).aspx">this link</a> for more information.
    internal enum BmpColorSpaceIntent
    {
        // Microsoft Windows BMP v5

        /// <summary>
        /// Saturation. Maintains saturation.
        /// Used for business charts and other situations in which undithered colors are required.
        /// </summary>
        Business = 1,

        /// <summary>
        /// Relative Colorimetric. Maintains colorimetric match.
        /// Used for graphic designs and named colors.
        /// </summary>
        Graphics = 2,

        /// <summary>
        /// Perceptual. Maintains contrast.
        /// Used for photographs and natural images.
        /// </summary>
        Images = 4,

        /// <summary>
        /// Absolute Colorimetric. Maintains the white point.
        /// Matches the colors to their nearest color in the destination gamut.
        /// </summary>
        AbsoluteColoriMetric = 8
    }
}
