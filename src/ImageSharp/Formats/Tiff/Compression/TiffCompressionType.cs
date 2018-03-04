// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Tiff
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

        /// <summary>
        /// Image data is compressed using Deflate compression.
        /// </summary>
        Deflate = 2,

        /// <summary>
        /// Image data is compressed using LZW compression.
        /// </summary>
        Lzw = 3,
    }
}
