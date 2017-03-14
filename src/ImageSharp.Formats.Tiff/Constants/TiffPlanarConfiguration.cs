// <copyright file="TiffCompression.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    /// <summary>
    /// Enumeration representing the planar configuration types defined by the Tiff file-format.
    /// </summary>
    internal enum TiffPlanarConfiguration
    {
        // TIFF baseline PlanarConfiguration values

        Chunky = 1,
        Planar = 2
    }
}