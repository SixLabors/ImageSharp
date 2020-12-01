// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Experimental.Tiff
{
    /// <summary>
    /// Indicates which tiff compression is used.
    /// </summary>
    public enum TiffEncoderCompression
    {
        /// <summary>
        /// No compression is used.
        /// </summary>
        None,

        /// <summary>
        /// Use zlib compression.
        /// </summary>
        Deflate,

        /// <summary>
        /// Use PackBits to compression the image data.
        /// </summary>
        PackBits,

        /// <summary>
        /// Use CCITT T4 1D compression. Note: This is only valid for bi-level images.
        /// </summary>
        CcittGroup3Fax,

        /// <summary>
        /// Use the modified Huffman RLE. Note: This is only valid for bi-level images.
        /// </summary>
        ModifiedHuffman,
    }
}
