// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

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
        /// Image data is compressed using CCITT T.4 fax compression.
        /// </summary>
        T4 = 4,

        /// <summary>
        /// Image data is compressed using CCITT T.6 fax compression.
        /// </summary>
        T6 = 5,

        /// <summary>
        /// Image data is compressed using modified huffman compression.
        /// </summary>
        HuffmanRle = 6,

        /// <summary>
        /// The image data is compressed as a JPEG stream.
        /// </summary>
        Jpeg = 7,

        /// <summary>
        /// The image data is compressed as a WEBP stream.
        /// </summary>
        Webp = 8,
    }
}
