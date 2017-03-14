// <copyright file="TiffExtraSamples.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    /// <summary>
    /// Enumeration representing the possible uses of extra-samples in TIFF format files.
    /// </summary>
    internal enum TiffExtraSamples
    {
        // TIFF baseline ExtraSample values

        Unspecified = 0,
        AssociatedAlpha = 1,
        UnassociatedAlpha = 2
    }
}