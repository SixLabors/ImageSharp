// <copyright file="IRgbWorkingSpace.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Colors.Spaces
{
    /// <summary>
    /// Encasulates the RGB working color space
    /// </summary>
    public interface IRgbWorkingSpace
    {
        /// <summary>
        /// Gets the reference white of the color space
        /// </summary>
        CieXyz WhitePoint { get; }

        // <summary>
        // Gets Chromaticity coordinates of the primaries
        // </summary>
        // RGBPrimariesChromaticityCoordinates ChromaticityCoordinates { get; }

        /// <summary>
        /// Gets the companding function associated with the RGB color system.
        /// Used for conversion to XYZ and backwards.
        /// See this for more information:
        /// http://www.brucelindbloom.com/index.html?Eqn_RGB_to_XYZ.html
        /// http://www.brucelindbloom.com/index.html?Eqn_XYZ_to_RGB.html
        /// </summary>
        ICompanding Companding { get; }
    }
}
