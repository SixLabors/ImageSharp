// <copyright file="TiffCompressionType.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats.Tiff
{
    /// <summary>
    /// Provides enumeration of the various TIFF compression types.
    /// </summary>
    internal enum TiffCompressionType
    {
        /// <summary>
        /// Image data is stored uncompressed in the TIFF file.
        /// </summary>
        None = 0,

        /// <summary>
        /// Image data is compressed using PackBits compression.
        /// </summary>
        PackBits = 1,
    }
}
