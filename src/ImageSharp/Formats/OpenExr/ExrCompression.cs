// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.OpenExr
{
    internal enum ExrCompression
    {
        /// <summary>
        /// Pixel data is not compressed.
        /// </summary>
        None = 0,

        /// <summary>
        /// Differences between horizontally adjacent pixels are run-length encoded.
        /// This method is fast, and works well for images with large flat areas, but for photographic images,
        /// the compressed file size is usually between 60 and 75 percent of the uncompressed size.
        /// Compression is lossless.
        /// </summary>
        RunLengthEncoded = 1,

        /// <summary>
        /// Uses the open source zlib library for compression. Unlike ZIP compression, this operates one scan line at a time.
        /// Compression is lossless.
        /// </summary>
        Zips = 2,

        /// <summary>
        /// Differences between horizontally adjacent pixels are compressed using the open source zlib library.
        /// Unlike ZIPS compression, this operates in in blocks of 16 scan lines.
        /// Compression is lossless.
        /// </summary>
        Zip = 3,

        /// <summary>
        /// A wavelet transform is applied to the pixel data, and the result is Huffman-encoded.
        /// Compression is lossless.
        /// </summary>
        Piz = 4,

        /// <summary>
        /// After reducing 32-bit floating-point data to 24 bits by rounding, differences between horizontally adjacent pixels are compressed with zlib,
        /// similar to ZIP. PXR24 compression preserves image channels of type HALF and UINT exactly, but the relative error of FLOAT data increases to about 3×10-5.
        /// Compression is lossy.
        /// </summary>
        Pxr24 = 5,

        /// <summary>
        /// Channels of type HALF are split into blocks of four by four pixels or 32 bytes. Each block is then packed into 14 bytes,
        /// reducing the data to 44 percent of their uncompressed size.
        /// Compression is lossy.
        /// </summary>
        B44 = 6,

        /// <summary>
        /// Like B44, except for blocks of four by four pixels where all pixels have the same value, which are packed into 3 instead of 14 bytes.
        /// For images with large uniform areas, B44A produces smaller files than B44 compression.
        /// Compression is lossy.
        /// </summary>
        B44A = 7
    }
}
