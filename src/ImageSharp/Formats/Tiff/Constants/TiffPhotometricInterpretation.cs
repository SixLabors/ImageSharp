// <copyright file="TiffPhotometricInterpretation.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    /// <summary>
    /// Enumeration representing the photometric interpretation formats defined by the Tiff file-format.
    /// </summary>
    internal enum TiffPhotometricInterpretation
    {
        // TIFF baseline color spaces

        WhiteIsZero = 0,
        BlackIsZero = 1,
        Rgb = 2,
        PaletteColor = 3,
        TransparencyMask = 4,

        // TIFF Extension color spaces

        Separated = 5,
        YCbCr = 6,
        CieLab = 8,

        // TIFF TechNote 1

        IccLab = 9,

        // TIFF-F/FX Specification

        ItuLab = 10,

        // DNG Specification

        ColorFilterArray = 32803,
        LinearRaw = 34892
    }
}