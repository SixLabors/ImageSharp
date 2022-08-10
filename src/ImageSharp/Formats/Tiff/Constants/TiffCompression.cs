// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Tiff.Constants
{
    /// <summary>
    /// Enumeration representing the compression formats defined by the Tiff file-format.
    /// </summary>
    public enum TiffCompression : ushort
    {
        /// <summary>
        /// A invalid compression value.
        /// </summary>
        Invalid = 0,

        /// <summary>
        /// No compression.
        /// </summary>
        None = 1,

        /// <summary>
        /// CCITT Group 3 1-Dimensional Modified Huffman run-length encoding.
        /// </summary>
        Ccitt1D = 2,

        /// <summary>
        /// T4-encoding: CCITT T.4 bi-level encoding (see Section 11 of the TIFF 6.0 specification).
        /// </summary>
        CcittGroup3Fax = 3,

        /// <summary>
        /// T6-encoding: CCITT T.6 bi-level encoding (see Section 11 of the TIFF 6.0 specification).
        /// </summary>
        CcittGroup4Fax = 4,

        /// <summary>
        /// LZW compression (see Section 13 of the TIFF 6.0 specification).
        /// </summary>
        Lzw = 5,

        /// <summary>
        /// JPEG compression - obsolete (see Section 22 of the TIFF 6.0 specification).
        ///
        /// Note: The TIFF encoder does not support this compression and will default to use no compression instead,
        /// if this is chosen.
        /// </summary>
        OldJpeg = 6,

        /// <summary>
        /// JPEG compression (see TIFF Specification, supplement 2).
        ///
        /// Note: The TIFF encoder does not yet support this compression and will default to use no compression instead,
        /// if this is chosen.
        /// </summary>
        Jpeg = 7,

        /// <summary>
        /// Deflate compression, using zlib data format (see TIFF Specification, supplement 2).
        /// </summary>
        Deflate = 8,

        /// <summary>
        /// ITU-T Rec. T.82 coding, applying ITU-T Rec. T.85 (JBIG) (see RFC2301).
        ///
        /// Note: The TIFF encoder does not yet support this compression and will default to use no compression instead,
        /// if this is chosen.
        /// </summary>
        ItuTRecT82 = 9,

        /// <summary>
        /// ITU-T Rec. T.43 representation, using ITU-T Rec. T.82 (JBIG) (see RFC2301).
        ///
        /// Note: The TIFF encoder does not yet support this compression and will default to use no compression instead,
        /// if this is chosen.
        /// </summary>
        ItuTRecT43 = 10,

        /// <summary>
        /// NeXT 2-bit Grey Scale compression algorithm.
        ///
        /// Note: The TIFF encoder does not support this compression and will default to use no compression instead,
        /// if this is chosen.
        /// </summary>
        NeXT = 32766,

        /// <summary>
        /// PackBits compression.
        /// </summary>
        PackBits = 32773,

        /// <summary>
        /// ThunderScan 4-bit compression.
        ///
        /// Note: The TIFF encoder does not support this compression and will default to use no compression instead,
        /// if this is chosen.
        /// </summary>
        ThunderScan = 32809,

        /// <summary>
        /// Deflate compression - old.
        ///
        /// Note: The TIFF encoder does not support this compression and will default to use no compression instead,
        /// if this is chosen.
        /// </summary>
        OldDeflate = 32946,

        /// <summary>
        /// Pixel data is compressed with webp encoder.
        ///
        /// Note: The TIFF encoder does not support this compression and will default to use no compression instead,
        /// if this is chosen.
        /// </summary>
        Webp = 50001,
    }
}
