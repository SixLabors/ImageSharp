// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;

namespace SixLabors.ImageSharp.Formats.WebP
{
    /// <summary>
    /// Constants used for decoding VP8 and VP8L bitstreams.
    /// </summary>
    internal static class WebPConstants
    {
        /// <summary>
        /// The list of file extensions that equate to WebP.
        /// </summary>
        public static readonly IEnumerable<string> FileExtensions = new[] { "webp" };

        /// <summary>
        /// The list of mimetypes that equate to a jpeg.
        /// </summary>
        public static readonly IEnumerable<string> MimeTypes = new[] { "image/webp", };

        /// <summary>
        /// Signature which identifies a VP8 header.
        /// </summary>
        public static readonly byte[] Vp8MagicBytes =
        {
            0x9D,
            0x01,
            0x2A
        };

        /// <summary>
        /// The header bytes identifying RIFF file.
        /// </summary>
        public static readonly byte[] RiffFourCc =
        {
            0x52, // R
            0x49, // I
            0x46, // F
            0x46 // F
        };

        /// <summary>
        /// The header bytes identifying a WebP.
        /// </summary>
        public static readonly byte[] WebPHeader =
        {
            0x57, // W
            0x45, // E
            0x42, // B
            0x50 // P
        };

        /// <summary>
        /// Signature byte which identifies a VP8L header.
        /// </summary>
        public const byte Vp8LMagicByte = 0x2F;

        /// <summary>
        /// 3 bits reserved for version.
        /// </summary>
        public const int Vp8LVersionBits = 3;

        /// <summary>
        /// Bits for width and height infos of a VPL8 image.
        /// </summary>
        public const int Vp8LImageSizeBits = 14;

        /// <summary>
        /// Maximum number of color cache bits.
        /// </summary>
        public const int MaxColorCacheBits = 11;

        /// <summary>
        /// The maximum number of allowed transforms in a VP8L bitstream.
        /// </summary>
        public const int MaxNumberOfTransforms = 4;

        public const int MaxAllowedCodeLength = 15;

        public const int DefaultCodeLength = 8;

        public const int HuffmanCodesPerMetaCode = 5;

        public const uint ArgbBlack = 0xff000000;

        public const int NumLiteralCodes = 256;

        public const int NumLengthCodes = 24;

        public const int NumDistanceCodes = 40;

        public const int LengthTableBits = 7;

        public const uint CodeLengthLiterals = 16;

        public const int CodeLengthRepeatCode = 16;

        public static readonly int[] CodeLengthExtraBits = { 2, 3, 7 };

        public static readonly int[] CodeLengthRepeatOffsets = { 3, 3, 11 };

        public static readonly int[] AlphabetSize =
        {
                                                         NumLiteralCodes + NumLengthCodes,
                                                         NumLiteralCodes, NumLiteralCodes, NumLiteralCodes,
                                                         NumDistanceCodes
        };

        // VP8 constants from here on:
        public const int NumMbSegments = 4;

        public const int MaxNumPartitions = 8;

        public const int NumTypes = 4;

        public const int NumBands = 8;

        public const int NumProbas = 11;

        public const int NumCtx = 3;

        // this is the common stride for enc/dec
        public const int Bps = 32;

        // intra prediction modes (TODO: maybe use an enum for this)
        public const int DcPred = 0; // predict DC using row above and column to the left
        public const int TmPred = 1; // propagate second differences a la "True Motion"
        public const int VPred = 2; // predict rows using row above
        public const int HPred = 3; // predict columns using column to the left

        /// <summary>
        /// How many extra lines are needed on the MB boundary for caching, given a filtering level.
        /// Simple filter(1):  up to 2 luma samples are read and 1 is written.
        /// Complex filter(2): up to 4 luma samples are read and 3 are written. Same for U/V, so it's 8 samples total (because of the 2x upsampling).
        /// </summary>
        public static readonly byte[] FilterExtraRows = { 0, 2, 8 };

        // Paragraph 9.9
        public static readonly int[] Bands =
        {
            0, 1, 2, 3, 6, 4, 5, 6, 6, 6, 6, 6, 6, 6, 6, 7, 0
        };

        public static readonly short[] Scan =
        {
            0 + (0 * Bps), 4 + (0 * Bps), 8 + (0 * Bps), 12 + (0 * Bps),
            0 + (4 * Bps), 4 + (4 * Bps), 8 + (4 * Bps), 12 + (4 * Bps),
            0 + (8 * Bps), 4 + (8 * Bps), 8 + (8 * Bps), 12 + (8 * Bps),
            0 + (12 * Bps), 4 + (12 * Bps), 8 + (12 * Bps), 12 + (12 * Bps)
        };

        // Residual decoding (Paragraph 13.2 / 13.3)
        public static readonly byte[] Cat3 = { 173, 148, 140 };
        public static readonly byte[] Cat4 = { 176, 155, 140, 135 };
        public static readonly byte[] Cat5 = { 180, 157, 141, 134, 130 };
        public static readonly byte[] Cat6 = { 254, 254, 243, 230, 196, 177, 153, 140, 133, 130, 129 };
        public static readonly byte[] Zigzag = { 0, 1, 4, 8,  5, 2, 3, 6,  9, 12, 13, 10,  7, 11, 14, 15 };

        public static readonly sbyte[] YModesIntra4 =
        {
            -0, 1,
            -1, 2,
            -2, 3,
            4, 6,
            -3, 5,
            -4, -5,
            -6, 7,
            -7, 8,
            -8, -9
        };
    }
}
