// <copyright file="TiffCompression.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats.Tiff
{
    /// <summary>
    /// Enumeration representing how the components of each pixel are stored the Tiff file-format.
    /// </summary>
    internal enum TiffPlanarConfiguration
    {
        /// <summary>
        /// Chunky format.
        /// </summary>
        Chunky = 1,

        /// <summary>
        /// Planar format.
        /// </summary>
        Planar = 2
    }
}