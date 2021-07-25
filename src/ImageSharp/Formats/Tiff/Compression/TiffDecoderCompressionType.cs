// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Tiff.Compression
{
    /// <summary>
    /// Provides enumeration of the various TIFF compression types the decoder can handle.
    /// </summary>
    internal enum TiffDecoderCompressionType
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

        /// <summary>
        /// Image data is compressed using T4-encoding: CCITT T.4.
        /// </summary>
        T4 = 4,

        /// <summary>
        /// Image data is compressed using modified huffman compression.
        /// </summary>
        HuffmanRle = 5,
    }
}
