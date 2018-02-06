// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation.RgbColorSapce;

namespace SixLabors.ImageSharp.ColorSpaces
{
    /// <summary>
    /// Encapsulates the RGB working color space
    /// </summary>
    internal interface IRgbWorkingSpace : IEquatable<IRgbWorkingSpace>
    {
        /// <summary>
        /// Gets the reference white of the color space
        /// </summary>
        CieXyz WhitePoint { get; }

        /// <summary>
        /// Gets the chromaticity coordinates of the primaries
        /// </summary>
        RgbPrimariesChromaticityCoordinates ChromaticityCoordinates { get; }

        /// <summary>
        /// Expands a compressed channel to its linear equivalent with respect to the energy.
        /// </summary>
        /// <remarks>
        /// For more info see:
        /// <see href="http://www.brucelindbloom.com/index.html?Eqn_RGB_to_XYZ.html"/>
        /// </remarks>
        /// <param name="channel">The channel value</param>
        /// <returns>The linear channel value</returns>
        float Expand(float channel);

        /// <summary>
        /// Compresses an expanded channel (linear) to its nonlinear equivalent (depends on the RGB color system).
        /// </summary>
        /// <remarks>
        /// For more info see:
        /// <see href="http://www.brucelindbloom.com/index.html?Eqn_XYZ_to_RGB.html"/>
        /// </remarks>
        /// <param name="channel">The channel value</param>
        /// <returns>The nonlinear channel value</returns>
        float Compress(float channel);
    }
}