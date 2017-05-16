// <copyright file="IChromaticAdaptation.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.ColorSpaces.Conversion
{
    using ImageSharp.ColorSpaces;

    /// <summary>
    /// Chromatic adaptation.
    /// A linear transformation of a source color (XS, YS, ZS) into a destination color (XD, YD, ZD) by a linear transformation [M]
    /// which is dependent on the source reference white (XWS, YWS, ZWS) and the destination reference white (XWD, YWD, ZWD).
    /// </summary>
    internal interface IChromaticAdaptation
    {
        /// <summary>
        /// Performs a linear transformation of a source color in to the destination color.
        /// </summary>
        /// <remarks>Doesn't crop the resulting color space coordinates (e. g. allows negative values for XYZ coordinates).</remarks>
        /// <param name="sourceColor">The source color.</param>
        /// <param name="sourceWhitePoint">The source white point.</param>
        /// <param name="targetWhitePoint">The target white point.</param>
        /// <returns>The <see cref="CieXyz"/></returns>
        CieXyz Transform(CieXyz sourceColor, CieXyz sourceWhitePoint, CieXyz targetWhitePoint);
    }
}