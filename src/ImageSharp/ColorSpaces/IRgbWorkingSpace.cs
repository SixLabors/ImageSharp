// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation.RgbColorSapce;

namespace SixLabors.ImageSharp.ColorSpaces
{
    /// <summary>
    /// Encasulates the RGB working color space
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
        /// Gets the companding function associated with the RGB color system. Used for conversion to XYZ and backwards.
        /// <see href="http://www.brucelindbloom.com/index.html?Eqn_RGB_to_XYZ.html"/>
        /// <see href="http://www.brucelindbloom.com/index.html?Eqn_XYZ_to_RGB.html"/>
        /// </summary>
        ICompanding Companding { get; }
    }
}