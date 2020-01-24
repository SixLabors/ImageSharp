// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;

namespace SixLabors.ImageSharp.Formats.WebP
{
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

        // VP8 constants
        public const int NumMbSegments = 4;

        public const int MaxNumPartitions = 8;

        // Paragraph 14.1
        public static readonly int[] DcTable =
        {
            4,     5,   6,   7,   8,   9,  10,  10,
            11,   12,  13,  14,  15,  16,  17,  17,
            18,   19,  20,  20,  21,  21,  22,  22,
            23,   23,  24,  25,  25,  26,  27,  28,
            29,   30,  31,  32,  33,  34,  35,  36,
            37,   37,  38,  39,  40,  41,  42,  43,
            44,   45,  46,  46,  47,  48,  49,  50,
            51,   52,  53,  54,  55,  56,  57,  58,
            59,   60,  61,  62,  63,  64,  65,  66,
            67,   68,  69,  70,  71,  72,  73,  74,
            75,   76,  76,  77,  78,  79,  80,  81,
            82,   83,  84,  85,  86,  87,  88,  89,
            91,   93,  95,  96,  98, 100, 101, 102,
            104, 106, 108, 110, 112, 114, 116, 118,
            122, 124, 126, 128, 130, 132, 134, 136,
            138, 140, 143, 145, 148, 151, 154, 157
        };

        // Paragraph 14.1
        public static readonly int[] AcTable =
        {
            4,     5,   6,   7,   8,   9,  10,  11,
            12,   13,  14,  15,  16,  17,  18,  19,
            20,   21,  22,  23,  24,  25,  26,  27,
            28,   29,  30,  31,  32,  33,  34,  35,
            36,   37,  38,  39,  40,  41,  42,  43,
            44,   45,  46,  47,  48,  49,  50,  51,
            52,   53,  54,  55,  56,  57,  58,  60,
            62,   64,  66,  68,  70,  72,  74,  76,
            78,   80,  82,  84,  86,  88,  90,  92,
            94,   96,  98, 100, 102, 104, 106, 108,
            110, 112, 114, 116, 119, 122, 125, 128,
            131, 134, 137, 140, 143, 146, 149, 152,
            155, 158, 161, 164, 167, 170, 173, 177,
            181, 185, 189, 193, 197, 201, 205, 209,
            213, 217, 221, 225, 229, 234, 239, 245,
            249, 254, 259, 264, 269, 274, 279, 284
        };

        // Paragraph 9.9
        public static readonly int[] Bands =
        {
            0, 1, 2, 3, 6, 4, 5, 6, 6, 6, 6, 6, 6, 6, 6, 7, 0
        };
    }
}
