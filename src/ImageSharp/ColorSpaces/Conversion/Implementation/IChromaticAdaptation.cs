// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion
{
    /// <summary>
    /// Chromatic adaptation.
    /// A linear transformation of a source color (XS, YS, ZS) into a destination color (XD, YD, ZD) by a linear transformation [M]
    /// which is dependent on the source reference white (XWS, YWS, ZWS) and the destination reference white (XWD, YWD, ZWD).
    /// </summary>
    public interface IChromaticAdaptation
    {
        /// <summary>
        /// Performs a linear transformation of a source color in to the destination color.
        /// </summary>
        /// <remarks>Doesn't crop the resulting color space coordinates (e. g. allows negative values for XYZ coordinates).</remarks>
        /// <param name="source">The source color.</param>
        /// <param name="sourceWhitePoint">The source white point.</param>
        /// <param name="destinationWhitePoint">The destination white point.</param>
        /// <returns>The <see cref="CieXyz"/></returns>
        CieXyz Transform(in CieXyz source, in CieXyz sourceWhitePoint, in CieXyz destinationWhitePoint);

        /// <summary>
        /// Performs a bulk linear transformation of a source color in to the destination color.
        /// </summary>
        /// <remarks>Doesn't crop the resulting color space coordinates (e. g. allows negative values for XYZ coordinates).</remarks>
        /// <param name="source">The span to the source colors.</param>
        /// <param name="destination">The span to the destination colors.</param>
        /// <param name="sourceWhitePoint">The source white point.</param>
        /// <param name="destinationWhitePoint">The destination white point.</param>
        void Transform(
            ReadOnlySpan<CieXyz> source,
            Span<CieXyz> destination,
            CieXyz sourceWhitePoint,
            in CieXyz destinationWhitePoint);
    }
}